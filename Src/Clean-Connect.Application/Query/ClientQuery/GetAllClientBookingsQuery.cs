using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Query.ClientQuery
{
    public record GetAllClientBookingsQuery(Guid ClientId) : IRequest<List<BookingDto>>;

    public class GetAllClientBookingsQueryHandler : IRequestHandler<GetAllClientBookingsQuery, List<BookingDto>>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<GetAllClientBookingsQuery> logger;

        public GetAllClientBookingsQueryHandler(IUnitOfWork _repo, ILogger<GetAllClientBookingsQuery> _logger)
        {
            repo = _repo;
            logger = _logger;
        }
        public async Task<List<BookingDto>> Handle(GetAllClientBookingsQuery request, CancellationToken cancellationToken)
        {
            if (request.ClientId == Guid.Empty)
                throw new KeyNotFoundException("Invalid Id");

            var check = await repo.Clients.GetClientById(request.ClientId, cancellationToken);

            if (check == null)
            {
                logger.LogWarning("GetClientByIdQueryHandler: Client with Id {ClientId} not found.", request.ClientId);
                throw new ArgumentException($"Client with Id {request.ClientId} not found.");
            }

            var bookings = check.Bookings.Select(b => new BookingDto
            {
                Id = b.Id,
                ServiceName = b.ServiceType.Name,
                ClientName = check.FullName,
                WorkersName = b.Worker.FullName,
                BookingDate = b.DateOfBooking,
                DateOfService = b.DateOfService,
                TimeRange = b.TimeRange.ToString(),
                Amount = b.Amount,
                Address = b.Address,
                PaymentStatus = b.PaymentStatus.ToString(),
                BookingStatus = b.BookingStatus.ToString()
            }).ToList();

            if(!bookings.Any())
            {
                logger.LogInformation("GetAllClientBookingsQueryHandler: No bookings found for Client with Id {ClientId}.", request.ClientId);
                
            }   

            logger.LogInformation("GetAllClientBookingsQueryHandler: Retrieved {Count} bookings for Client with Id {ClientId}.", bookings.Count, request.ClientId);

            return bookings;
        }
    }


}
