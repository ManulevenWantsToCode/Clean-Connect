using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record DeletePaymentCommand(Guid PaymentId, string? ModifiedBy = null) : IRequest<bool>;

    public class DeletePaymentCommandHandler : IRequestHandler<DeletePaymentCommand, bool>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<DeletePaymentCommandHandler> logger;

        public DeletePaymentCommandHandler(IUnitOfWork repo, ILogger<DeletePaymentCommandHandler> logger)
        {
            this.repo = repo;
            this.logger = logger;
        }

        public async Task<bool> Handle(DeletePaymentCommand request, CancellationToken cancellationToken)
        {
            if (request.PaymentId == Guid.Empty)
            {
                logger.LogWarning("Invalid payment ID provided for deletion");
                throw new ArgumentException("Payment ID cannot be empty.", nameof(request.PaymentId));
            }

            var payment = await repo.Payments.GetPaymentById(request.PaymentId, cancellationToken);
            if (payment == null)
            {
                logger.LogWarning("Payment with ID {PaymentId} not found for deletion", request.PaymentId);
                throw new KeyNotFoundException($"Payment with ID {request.PaymentId} not found.");
            }

            try
            {
                await repo.Payments.DeletePayment(request.PaymentId, cancellationToken);
                logger.LogInformation("Payment with ID {PaymentId} deleted successfully by {ModifiedBy}", request.PaymentId, request.ModifiedBy ?? "Unknown");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError("Error deleting payment {PaymentId}; exception type {ExceptionType}.", request.PaymentId, ex.GetType().Name);
                throw;
            }
        }
    }
}
