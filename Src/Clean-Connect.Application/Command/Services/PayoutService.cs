using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.Services
{
    /// <summary>
    /// Orchestrates direct Paystack bank transfers to workers on job completion.
    /// Transfers are automatic once the worker has stored payout bank details;
    /// otherwise they remain pending (Held escrow) and complete automatically
    /// when bank details are added. Transfer failures never break the completion flow.
    /// </summary>
    public class PayoutService
    {
        private readonly IUnitOfWork _repo;
        private readonly IPaystackService _paystackService;
        private readonly ILogger<PayoutService> _logger;

        public PayoutService(
            IUnitOfWork repo,
            IPaystackService paystackService,
            ILogger<PayoutService> logger)
        {
            _repo = repo;
            _paystackService = paystackService;
            _logger = logger;
        }

        public async Task<PayoutResult> PayoutAsync(Booking booking, string? modifiedBy, CancellationToken cancellationToken)
            => await PayoutAsync(booking, modifiedBy, reconcileExisting: false, cancellationToken);

        private async Task<PayoutResult> PayoutAsync(Booking booking, string? modifiedBy, bool reconcileExisting, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting payout orchestration for booking: {BookingId}, worker: {WorkerId}", booking.Id, booking.WorkerId);

            if (booking.PaymentStatus != PaymentStatus.Successful)
            {
                _logger.LogWarning("Payout aborted for booking {BookingId}. Payment status is {PaymentStatus}, not Successful.", booking.Id, booking.PaymentStatus);
                return new PayoutResult(false, "Booking must be paid before payout.");
            }

            if (booking.BookingStatus != BookingStatus.Completed)
            {
                _logger.LogWarning("Payout aborted for booking {BookingId}. Booking status is {BookingStatus}, not Completed.", booking.Id, booking.BookingStatus);
                return new PayoutResult(false, "Booking must be completed before payout.");
            }

            var escrow = await _repo.Escrows.GetByBookingId(booking.Id, cancellationToken);
            if (escrow == null)
            {
                _logger.LogError("Escrow not found for booking: {BookingId}", booking.Id);
                throw new InvalidOperationException($"Escrow not found for booking {booking.Id}");
            }

            if (escrow.Status == EscrowStatus.PaidOut)
            {
                if (escrow.TransferStatus == PayoutTransferStatus.LegacyUnverified)
                    return await ReconcileLegacyPayoutAsync(escrow, modifiedBy, cancellationToken);
                return new PayoutResult(true, "This payout has been confirmed by Paystack.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());
            }

            if (escrow.Status != EscrowStatus.Held)
            {
                return new PayoutResult(false, $"Escrow cannot be paid out from {escrow.Status} status.");
            }

            var bankDetail = await _repo.WorkerBankDetails.GetActiveByWorkerIdAsync(booking.WorkerId, cancellationToken);
            if (bankDetail == null)
            {
                _logger.LogInformation("Worker {WorkerId} has no bank details yet. Booking {BookingId} payout deferred until bank details are added.", booking.WorkerId, booking.Id);
                return new PayoutResult(false, "Worker has not added payout bank details. The transfer will be completed automatically once bank details are added.");
            }

            if (!reconcileExisting && escrow.TransferStatus == PayoutTransferStatus.Pending)
                return new PayoutResult(false, "Paystack is processing this transfer. The payout will update when Paystack confirms its final status.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());
            if (!reconcileExisting && escrow.TransferStatus == PayoutTransferStatus.OtpRequired)
                return new PayoutResult(false, "Paystack requires OTP approval before this transfer can continue. Approve it in Paystack, then wait for the transfer webhook.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());

            return await PayoutToExternalTransferAsync(booking, bankDetail, escrow, modifiedBy, cancellationToken);
        }

        public async Task PayoutPendingForWorkerAsync(Guid workerId, string? modifiedBy, CancellationToken cancellationToken)
        {
            var escrows = await _repo.Escrows.GetByWorkerId(workerId, cancellationToken);
            foreach (var escrow in escrows.Where(x => x.Status == EscrowStatus.Held
                || x.TransferStatus == PayoutTransferStatus.LegacyUnverified).ToList())
            {
                var booking = await _repo.Bookings.GetBookingById(escrow.BookingId, cancellationToken);
                if (booking == null || booking.BookingStatus != BookingStatus.Completed || booking.PaymentStatus != PaymentStatus.Successful)
                {
                    continue;
                }

                var result = await PayoutAsync(booking, modifiedBy, reconcileExisting: true, cancellationToken);
                _logger.LogInformation("Payout reconciliation completed for booking {BookingId} with success {Success}.", booking.Id, result.Success);
            }
        }

        private async Task<PayoutResult> ReconcileLegacyPayoutAsync(Escrow escrow, string? modifiedBy, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(escrow.PaystackTransferCode))
                return new PayoutResult(false, "This older payout has no Paystack transfer code. Verify it manually in Paystack before taking any action.", null, escrow.TransferStatus.ToString());

            try
            {
                var transfer = await _paystackService.FetchTransferAsync(escrow.PaystackTransferCode, cancellationToken);
                if (transfer == null)
                    return new PayoutResult(false, "Paystack could not find this older transfer. It was not retried to avoid a duplicate payment; review it manually.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());

                var expectedMinorUnits = decimal.Round(escrow.WorkerShare * 100m, 0, MidpointRounding.AwayFromZero);
                if (transfer.Amount != expectedMinorUnits)
                    return new PayoutResult(false, "The Paystack transfer amount does not match escrow. Manual review is required.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());

                await _repo.Escrows.RefreshAsync(escrow, cancellationToken);
                if (escrow.TransferStatus == PayoutTransferStatus.LegacyUnverified)
                {
                    escrow.ReconcileLegacyPayout(transfer.Status, transfer.TransferCode, modifiedBy);
                    await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
                    await _repo.SaveChangesAsync(cancellationToken);
                }

                var completed = escrow.Status == EscrowStatus.PaidOut
                    && escrow.TransferStatus == PayoutTransferStatus.Succeeded;
                return new PayoutResult(completed, completed
                    ? "Paystack confirmed this legacy payout."
                    : $"Legacy transfer status from Paystack: {transfer.Status}.", transfer.TransferCode, escrow.TransferStatus.ToString());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconcile legacy payout for escrow {EscrowId}.", escrow.Id);
                return new PayoutResult(false, "Legacy payout status could not be verified. No new transfer was created.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());
            }
        }

        private async Task<PayoutResult> PayoutToExternalTransferAsync(Booking booking, WorkerBankDetail bankDetail, Escrow escrow, string? modifiedBy, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing payout for booking {BookingId}.", booking.Id);

            var wasUnresolved = escrow.TransferStatus is PayoutTransferStatus.Initiating
                or PayoutTransferStatus.Unknown
                or PayoutTransferStatus.Pending
                or PayoutTransferStatus.OtpRequired;
            var verifyOnly = escrow.TransferStatus == PayoutTransferStatus.OtpRequired;
            try
            {
                var recipientCode = bankDetail.RecipientCode;
                if (string.IsNullOrWhiteSpace(recipientCode))
                {
                    _logger.LogDebug("Creating transfer recipient for worker: {WorkerId}", booking.WorkerId);
                    var bankAccount = new WorkerBankAccountDto(
                        bankDetail.AccountNumber,
                        bankDetail.BankCode,
                        bankDetail.AccountName,
                        bankDetail.Currency);
                    var recipient = await _paystackService.CreateTransferRecipientAsync(bankAccount, cancellationToken);
                    recipientCode = recipient.RecipientCode;
                    bankDetail.SetRecipientCode(recipientCode, modifiedBy);
                    await _repo.WorkerBankDetails.UpdateAsync(bankDetail, cancellationToken);
                    await _repo.SaveChangesAsync(cancellationToken);
                }

                if (escrow.WorkerShare <= 0)
                    return new PayoutResult(false, "The worker payout amount must be greater than zero.");

                var reference = wasUnresolved
                    ? escrow.PaystackTransferReference
                    : escrow.BeginPayoutAttempt(modifiedBy);
                if (string.IsNullOrWhiteSpace(reference))
                    throw new InvalidOperationException("The payout is missing its persisted transfer reference.");
                if (!wasUnresolved)
                {
                    await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
                    await _repo.SaveChangesAsync(cancellationToken);
                }

                TransferInitiationResponse? transferResult = null;
                if (wasUnresolved)
                    transferResult = await _paystackService.VerifyTransferAsync(reference, cancellationToken);

                if (transferResult == null)
                {
                    if (verifyOnly)
                        return new PayoutResult(false, "This transfer is awaiting OTP approval in Paystack; no duplicate transfer was created.", escrow.PaystackTransferCode);

                    var reason = $"Payout for booking {booking.Id}";
                    _logger.LogInformation("Initiating payout transfer for booking {BookingId}.", booking.Id);
                    transferResult = await _paystackService.InitiateTransferAsync(recipientCode, escrow.WorkerShare, reason, reference, cancellationToken);
                }

                await _repo.Escrows.RefreshAsync(escrow, cancellationToken);
                if (escrow.Status != EscrowStatus.PaidOut)
                {
                    escrow.RecordPayoutTransfer(transferResult.TransferCode, transferResult.Status, modifiedBy);
                    await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
                    await _repo.SaveChangesAsync(cancellationToken);
                }

                var completed = escrow.Status == EscrowStatus.PaidOut;
                var message = escrow.TransferStatus switch
                {
                    PayoutTransferStatus.Succeeded => "Paystack confirmed the transfer.",
                    PayoutTransferStatus.Pending => "Paystack accepted the transfer; awaiting final confirmation.",
                    PayoutTransferStatus.OtpRequired => "Paystack requires OTP approval before the transfer can continue.",
                    PayoutTransferStatus.Failed => "Paystack rejected the transfer. It is eligible for a safe retry.",
                    PayoutTransferStatus.Reversed => "Paystack reversed the transfer. It is eligible for a safe retry.",
                    _ => "The transfer outcome is not yet confirmed. The same transfer reference will be checked before retrying."
                };
                _logger.LogInformation("Payout state for booking {BookingId} is {Status}.", booking.Id, escrow.TransferStatus);
                return new PayoutResult(completed, message, transferResult.TransferCode, escrow.TransferStatus.ToString());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct transfer payout failed for booking: {BookingId}. Payout deferred for retry.", booking.Id);
                try
                {
                    await _repo.Escrows.RefreshAsync(escrow, cancellationToken);
                    if (escrow.Status == EscrowStatus.PaidOut)
                        return new PayoutResult(true, "Paystack confirmed the transfer.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());
                    if (escrow.Status != EscrowStatus.PaidOut)
                    {
                        escrow.MarkPayoutUnknown(modifiedBy);
                        await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
                        await _repo.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception persistenceException)
                {
                    _logger.LogError(persistenceException, "Could not persist the unknown payout state for booking {BookingId}.", booking.Id);
                }
                return new PayoutResult(false, "Paystack did not return a conclusive result. The payout is saved for reconciliation and will reuse its transfer reference.", escrow.PaystackTransferCode, escrow.TransferStatus.ToString());
            }
        }

    }
}
