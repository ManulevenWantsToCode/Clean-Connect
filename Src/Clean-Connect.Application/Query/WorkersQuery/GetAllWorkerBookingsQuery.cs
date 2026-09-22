using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Query.WorkersQuery
{
    public record GetAllWorkerBookingsQuery(Guid WorkerId) : IRequest<List<BookingDto>>;

    public class GetAllWorkerBookingsQuerHandler : IRequestHandler<GetAllWorkerBookingsQuery, List<BookingDto>>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<GetAllWorkerBookingsQuerHandler> logger;

        public GetAllWorkerBookingsQuerHandler(IUnitOfWork _repo, ILogger<GetAllWorkerBookingsQuerHandler> _logger)
        {
            repo = _repo;
            logger = _logger;
        }

        public async Task<List<BookingDto>> Handle(GetAllWorkerBookingsQuery request, CancellationToken cancellationToken)
        {

            if (request.WorkerId == Guid.Empty)
                throw new KeyNotFoundException("Invalid Id");

            var check = await repo.Workers.GetWorkerById(request.WorkerId, cancellationToken);

            if (check == null)
            {
                logger.LogWarning("GetWorkerByIdQueryHandler: Worker with Id {WorkerId} not found.", request.WorkerId);
                throw new ArgumentException($"Worker with Id {request.WorkerId} not found.");
            }

            var bookings = check.Bookings.Select(b => new BookingDto
            {
                Id = b.Id,
                ServiceName = b.ServiceType.Name,
                ClientName = b.Client.FullName,
                WorkersName = b.Worker.FullName,
                BookingDate = b.DateOfBooking,
                DateOfService = b.DateOfService,
                TimeRange = b.TimeRange.ToString(),
                Amount = b.Amount,
                Rating = b.Ratings?.RatingValue ?? 0,
                Address = b.Address,
                PaymentStatus = b.PaymentStatus.ToString(),
                BookingStatus = b.BookingStatus.ToString()
            }).ToList();

            if (!bookings.Any())
            {
                logger.LogInformation("GetAllWorkerBookingsQuery: No bookings found for Worker with Id {WorkerId}.", request.WorkerId);

            }

            logger.LogInformation("GetAllWorkerBookingsQuery: Retrieved {Count} bookings for Worker with Id {WorkerId}.", bookings.Count, request.WorkerId);

            return bookings;
        }
    }


}
