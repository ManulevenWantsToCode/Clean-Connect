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
                return new PayoutResult(false, "This payout has already been transferred to the worker bank account.", escrow.PaystackTransferCode);
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

            return await PayoutToExternalTransferAsync(booking, bankDetail, escrow, modifiedBy, cancellationToken);
        }

        public async Task PayoutPendingForWorkerAsync(Guid workerId, string? modifiedBy, CancellationToken cancellationToken)
        {
            var bankDetail = await _repo.WorkerBankDetails.GetActiveByWorkerIdAsync(workerId, cancellationToken);
            if (bankDetail == null)
            {
                _logger.LogInformation("Worker {WorkerId} has no bank details yet; no pending payouts processed.", workerId);
                return;
            }

            var escrows = await _repo.Escrows.GetByWorkerId(workerId, cancellationToken);
            var paid = 0;
            foreach (var escrow in escrows.Where(x => x.Status == EscrowStatus.Held).ToList())
            {
                var booking = await _repo.Bookings.GetBookingById(escrow.BookingId, cancellationToken);
                if (booking.BookingStatus != BookingStatus.Completed || booking.PaymentStatus != PaymentStatus.Successful)
                {
                    continue;
                }

                var result = await PayoutAsync(booking, modifiedBy, cancellationToken);
                if (result.Success)
                {
                    paid++;
                }
                else
                {
                    _logger.LogInformation("Pending payout for booking {BookingId} not completed: {Message}", booking.Id, result.Message);
                }
            }

            _logger.LogInformation("Processed pending payouts for worker {WorkerId}: {Paid} paid out.", workerId, paid);
        }

        private async Task<PayoutResult> PayoutToExternalTransferAsync(Booking booking, WorkerBankDetail bankDetail, Escrow escrow, string? modifiedBy, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing direct transfer payout for booking: {BookingId}, account tail: {Tail}", booking.Id, Tail(bankDetail.AccountNumber));

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
                }

                var reason = $"Payout for booking {booking.Id}";
                _logger.LogDebug("Initiating transfer for booking: {BookingId}, amount: {Amount}", booking.Id, escrow.WorkerShare);
                var transferResult = await _paystackService.InitiateTransferAsync(recipientCode, escrow.WorkerShare, reason, cancellationToken);
                _logger.LogInformation("Transfer initiated. TransferCode: {TransferCode}, Status: {Status}", transferResult.TransferCode, transferResult.Status);

                escrow.MarkPaidOut(transferResult.TransferCode, modifiedBy);
                await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
                await _repo.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Direct transfer payout completed for booking: {BookingId}. ProviderRef: {ProviderRef}", booking.Id, transferResult.TransferCode);
                return new PayoutResult(true, "Transfer initiated to worker bank account.", transferResult.TransferCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct transfer payout failed for booking: {BookingId}. Payout deferred for retry.", booking.Id);
                return new PayoutResult(false, "Transfer could not be completed right now. It will be retried automatically.");
            }
        }

        private static string Tail(string accountNumber)
        {
            if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
                return accountNumber ?? string.Empty;
            return accountNumber[^4..];
        }
    }
}