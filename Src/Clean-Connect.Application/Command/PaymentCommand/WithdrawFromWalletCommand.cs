using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record WithdrawFromWalletCommand(
        Guid WorkerId,
        decimal Amount,
        string AccountNumber,
        string BankCode,
        string AccountName,
        string Currency = "NGN",
        string? ModifiedBy = null) : IRequest<PayoutResult>;

    public class WithdrawFromWalletCommandValidator : AbstractValidator<WithdrawFromWalletCommand>
    {
        public WithdrawFromWalletCommandValidator()
        {
            RuleFor(x => x.WorkerId)
                .NotEmpty()
                .WithMessage("WorkerId is required.");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Amount must be greater than zero.");

            RuleFor(x => x.AccountNumber)
                .NotEmpty()
                .WithMessage("AccountNumber is required.");

            RuleFor(x => x.BankCode)
                .NotEmpty()
                .WithMessage("BankCode is required.");

            RuleFor(x => x.AccountName)
                .NotEmpty()
                .WithMessage("AccountName is required.");
        }
    }

    public class WithdrawFromWalletCommandHandler(
        IUnitOfWork repo,
        IPaystackService paystackService,
        ILogger<WithdrawFromWalletCommandHandler> logger) : IRequestHandler<WithdrawFromWalletCommand, PayoutResult>
    {
        public async Task<PayoutResult> Handle(WithdrawFromWalletCommand request, CancellationToken cancellationToken)
        {
            var wallet = await repo.Wallets.GetByWorkerId(request.WorkerId, cancellationToken);
            if (wallet == null)
            {
                logger.LogWarning("Withdrawal failed. Wallet for worker {WorkerId} not found.", request.WorkerId);
                return new PayoutResult(false, "Wallet not found for this worker.");
            }

            if (wallet.Balance < request.Amount)
            {
                logger.LogWarning(
                    "Withdrawal failed. Insufficient balance for worker {WorkerId}: requested {Amount}, balance {Balance}.",
                    request.WorkerId, request.Amount, wallet.Balance);
                return new PayoutResult(false, "Insufficient wallet balance for this withdrawal.");
            }

            var bankAccount = new WorkerBankAccountDto(
                request.AccountNumber,
                request.BankCode,
                request.AccountName,
                request.Currency);

            logger.LogDebug("Creating transfer recipient for worker {WorkerId}.", request.WorkerId);
            var recipient = await paystackService.CreateTransferRecipientAsync(bankAccount, cancellationToken);
            logger.LogInformation("Transfer recipient created. RecipientCode: {RecipientCode}", recipient.RecipientCode);

            var reason = $"Withdrawal for worker {request.WorkerId}";
            logger.LogDebug("Initiating transfer for worker {WorkerId}, amount: {Amount}", request.WorkerId, request.Amount);
            var transferResult = await paystackService.InitiateTransferAsync(recipient.RecipientCode, request.Amount, reason, cancellationToken);
            logger.LogInformation("Transfer initiated. TransferCode: {TransferCode}, Status: {Status}", transferResult.TransferCode, transferResult.Status);

            wallet.Debit(request.Amount, request.ModifiedBy);
            await repo.Wallets.UpdateWallet(wallet, cancellationToken);
            await repo.SaveChangesAsync(cancellationToken);

            return new PayoutResult(true, "Withdrawal initiated.", transferResult.TransferCode);
        }
    }
}
