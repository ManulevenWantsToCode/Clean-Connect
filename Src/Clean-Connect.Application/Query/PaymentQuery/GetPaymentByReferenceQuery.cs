using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Query.PaymentQuery
{
    public record GetPaymentByReferenceQuery(string reference) : IRequest<PaymentDto>;

    public class GetPaymentByReferenceQueryHander : IRequestHandler<GetPaymentByReferenceQuery, PaymentDto>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<GetPaymentByReferenceQueryHander> logger;

        public GetPaymentByReferenceQueryHander(IUnitOfWork _repo, ILogger<GetPaymentByReferenceQueryHander> _logger)
        {
            repo = _repo;
            logger = _logger;
        }

        public async Task<PaymentDto> Handle(GetPaymentByReferenceQuery request, CancellationToken cancellationToken)
        {
            var payment = await repo.Payments.GetByReferenceAsync(request.reference, cancellationToken);
            if (payment == null)
            {
                logger.LogWarning("Payment lookup by reference did not match a payment record.");
                throw new KeyNotFoundException($"Payment with reference {request.reference} not found.");
            }
            return new PaymentDto
            {
                Id = payment.Id,
                BookingId = payment.BookingId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                Status = payment.Status,
                PaymentReference = payment.PaymentReference,
                CreatedAt = payment.DateCreated
            };
        }



    }


}
