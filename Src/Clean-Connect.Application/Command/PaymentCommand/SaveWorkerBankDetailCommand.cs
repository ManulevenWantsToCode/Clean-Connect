using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record SaveWorkerBankDetailCommand(
        Guid WorkerId,
        string BankCode,
        string BankName,
        string AccountNumber,
        string AccountName,
        string? Currency,
        string? ModifiedBy = null) : IRequest<bool>;

    public class SaveWorkerBankDetailCommandValidator : AbstractValidator<SaveWorkerBankDetailCommand>
    {
        public SaveWorkerBankDetailCommandValidator()
        {
            RuleFor(x => x.WorkerId).NotEmpty();
            RuleFor(x => x.BankCode).NotEmpty().MaximumLength(20);
            RuleFor(x => x.AccountNumber).NotEmpty().Length(10).Matches("^[0-9]+$");
            RuleFor(x => x.AccountName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.BankName).MaximumLength(100);
        }
    }

    public class SaveWorkerBankDetailCommandHandler : IRequestHandler<SaveWorkerBankDetailCommand, bool>
    {
        private readonly IUnitOfWork _repo;
        private readonly PayoutService _payoutService;
        private readonly ILogger<SaveWorkerBankDetailCommandHandler> _logger;

        public SaveWorkerBankDetailCommandHandler(IUnitOfWork repo, PayoutService payoutService, ILogger<SaveWorkerBankDetailCommandHandler> logger)
        {
            _repo = repo;
            _payoutService = payoutService;
            _logger = logger;
        }

        public async Task<bool> Handle(SaveWorkerBankDetailCommand request, CancellationToken cancellationToken)
        {
            var worker = await _repo.Workers.GetWorkerById(request.WorkerId, cancellationToken)
                ?? throw new InvalidOperationException($"Worker {request.WorkerId} was not found.");

            var existing = await _repo.WorkerBankDetails.GetActiveByWorkerIdAsync(request.WorkerId, cancellationToken);

            if (existing == null)
            {
                var detail = WorkerBankDetail.Create(
                    request.WorkerId,
                    request.BankCode,
                    request.BankName ?? string.Empty,
                    request.AccountNumber,
                    request.AccountName,
                    request.Currency ?? "NGN",
                    request.ModifiedBy);

                await _repo.WorkerBankDetails.CreateAsync(detail, cancellationToken);
            }
            else
            {
                existing.Update(
                    request.BankCode,
                    request.BankName ?? string.Empty,
                    request.AccountNumber,
                    request.AccountName,
                    request.Currency ?? "NGN",
                    request.ModifiedBy);

                await _repo.WorkerBankDetails.UpdateAsync(existing, cancellationToken);
            }

            await _repo.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved bank details for worker {WorkerId}", request.WorkerId);

            await _payoutService.PayoutPendingForWorkerAsync(request.WorkerId, request.ModifiedBy, cancellationToken);

            return true;
        }
    }
}