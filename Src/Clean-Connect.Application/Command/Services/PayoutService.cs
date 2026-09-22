using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.Services
{
    /// <summary>
    /// Orchestrates payout workflow: determines internal wallet credit vs external transfer,
    /// delegates to PaystackService for provider calls, manages escrow release, and persists changes.
    /// </summary>
    public class PayoutService
    {
        private readonly IUnitOfWork _repo;
        private readonly IPaystackService _paystackService;
        private readonly WalletService _walletService;
        private readonly EscrowService _escrowService;
        private readonly ILogger<PayoutService> _logger;

        public PayoutService(
            IUnitOfWork repo,
            IPaystackService paystackService,
            WalletService walletService,
            EscrowService escrowService,
            ILogger<PayoutService> logger)
        {
            _repo = repo;
            _paystackService = paystackService;
            _walletService = walletService;
            _escrowService = escrowService;
            _logger = logger;
        }

        public async Task<PayoutResult> PayoutAsync(Booking booking, WorkerBankAccountDto? bankAccount, string? modifiedBy, CancellationToken cancellationToken)
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

            if (bankAccount == null)
            {
                _logger.LogInformation("No bank account provided for booking {BookingId}. Using internal wallet payout.", booking.Id);
                return await PayoutToInternalWalletAsync(booking, escrow, modifiedBy, cancellationToken);
            }

            _logger.LogInformation("Bank account provided for booking {BookingId}. Initiating external transfer payout.", booking.Id);
            return await PayoutToExternalTransferAsync(booking, bankAccount, escrow, modifiedBy, cancellationToken);
        }

        private async Task<PayoutResult> PayoutToInternalWalletAsync(Booking booking, Escrow escrow, string? modifiedBy, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing internal wallet payout for booking: {BookingId}", booking.Id);

            try
            {
                if (escrow.Status == EscrowStatus.PaidOut)
                {
                    return new PayoutResult(false, "Booking payout has already been transferred to the worker bank account.", escrow.PaystackTransferCode);
                }

                if (escrow.Status == EscrowStatus.Released)
                {
                    return new PayoutResult(true, "Escrow has already been released to the worker wallet.", null);
                }

                if (escrow.Status != EscrowStatus.Held)
                {
                    return new PayoutResult(false, $"Escrow cannot be released from {escrow.Status} status.", null);
                }

                await _escrowService.ReleaseEscrowToWorkerWalletAsync(booking, modifiedBy, cancellationToken);
                await _repo.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Internal wallet payout completed successfully for booking: {BookingId}. Amount: {Amount}", booking.Id, escrow.Amount);
                return new PayoutResult(true, "Credited worker internal wallet.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Internal wallet payout failed for booking: {BookingId}", booking.Id);
                throw;
            }
        }

        private async Task<PayoutResult> PayoutToExternalTransferAsync(Booking booking, WorkerBankAccountDto bankAccount, Escrow escrow, string? modifiedBy, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing external transfer payout for booking: {BookingId}, account: {AccountName}", booking.Id, bankAccount.AccountName);

            try
            {
                if (escrow.Status == EscrowStatus.PaidOut)
                {
                    return new PayoutResult(false, "Booking payout has already been transferred to the worker bank account.", escrow.PaystackTransferCode);
                }

                if (escrow.Status != EscrowStatus.Released)
                {
                    return new PayoutResult(false, "Escrow must be released to the worker wallet before bank payout.", null);
                }

                var wallet = await _repo.Wallets.GetByWorkerId(booking.WorkerId, cancellationToken)
                    ?? throw new InvalidOperationException($"Wallet for worker {booking.WorkerId} was not found.");

                if (wallet.Balance < escrow.WorkerShare)
                {
                    return new PayoutResult(false, "Worker wallet balance is insufficient for this payout.", null);
                }

                _logger.LogDebug("Creating transfer recipient for booking: {BookingId}", booking.Id);
                var recipient = await _paystackService.CreateTransferRecipientAsync(bankAccount, cancellationToken);
                _logger.LogInformation("Transfer recipient created. RecipientCode: {RecipientCode}", recipient.RecipientCode);

                var reason = $"Payout for booking {booking.Id}";
                _logger.LogDebug("Initiating transfer for booking: {BookingId}, amount: {Amount}", booking.Id, escrow.WorkerShare);
                var transferResult = await _paystackService.InitiateTransferAsync(recipient.RecipientCode, escrow.WorkerShare, reason, cancellationToken);
                _logger.LogInformation("Transfer initiated. TransferCode: {TransferCode}, Status: {Status}", transferResult.TransferCode, transferResult.Status);

                wallet.Debit(escrow.WorkerShare, modifiedBy);
                escrow.MarkPaidOut(transferResult.TransferCode, modifiedBy);

                await _repo.Wallets.UpdateWallet(wallet, cancellationToken);
                await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
                await _repo.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("External transfer payout completed successfully for booking: {BookingId}. ProviderRef: {ProviderRef}", 
                    booking.Id, transferResult.TransferCode);
                return new PayoutResult(true, "External transfer initiated.", transferResult.TransferCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External transfer payout failed for booking: {BookingId}", booking.Id);
                throw;
            }
        }
    }
}
