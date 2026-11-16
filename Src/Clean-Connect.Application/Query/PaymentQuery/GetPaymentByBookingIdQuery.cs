using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Query.PaymentQuery
{
    public record GetPaymentByBookingIdQuery(Guid BookingId) : IRequest<List<PaymentDto>>;

    public class GetPaymentByBookingIdHandler : IRequestHandler<GetPaymentByBookingIdQuery, List<PaymentDto>>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<GetPaymentByBookingIdHandler> logger;

        public GetPaymentByBookingIdHandler(IUnitOfWork _repo, ILogger<GetPaymentByBookingIdHandler> _logger)
        {
            repo = _repo;
            logger = _logger;
        }

        public async Task<List<PaymentDto>> Handle(GetPaymentByBookingIdQuery request, CancellationToken cancellationToken)
        {
            if(request.BookingId == Guid.Empty)
            {
                logger.LogWarning("Invalid booking ID for payment lookup: {BookingId}", request.BookingId);
                throw new Exception("Invalid key");
            }
            var payments = await repo.Payments.GetPaymentsByBookingId(request.BookingId, cancellationToken);
            if (payments == null || !payments.Any())
            {
                logger.LogWarning("Payment for booking {BookingId} not found", request.BookingId);
                throw new KeyNotFoundException($"Payment for booking {request.BookingId} not found.");
            }
            return payments.Select(payment => new PaymentDto
            {
                Id = payment.Id,
                BookingId = payment.BookingId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                Status = payment.Status,
                PaymentReference = payment.PaymentReference,
            }).ToList();
        }
    }
}
