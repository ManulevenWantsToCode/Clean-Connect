using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Helper;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Clean_Connect.Persistence.Repositories
{
    public class WorkerRepository(ApplicationDbContext dbContext) : IWorkerRepository
    {
        public async Task CreateWorker(Worker worker, CancellationToken cancellationToken)
        {
            await dbContext.Workers.AddAsync(worker);
        }

        public async Task <Worker> GetWorkerByName(string name, CancellationToken cancellationToken)
        {
          return await dbContext.Workers.FindAsync(name.Trim(), cancellationToken);
        }

        public async Task <Worker> GetByEmail(string email, CancellationToken cancellationToken)
        {
            return await dbContext.Workers.FirstOrDefaultAsync(x  => x.Email.Value == email.Trim());
            
        }
        public async Task<Worker> GetWorkerById(Guid workerId, CancellationToken cancellationToken)
        {
            return await dbContext.Workers
                .Include(s => s.ServiceType)
                .Include(s => s.Bookings)
                .ThenInclude(c => c.Client)
                .Include(s => s.Bookings)
                .ThenInclude(c => c.Ratings)
                .Include(s => s.Bookings)
                .ThenInclude(c => c.ServiceType)        
                .FirstOrDefaultAsync(x => x.Id == workerId);
        }

        public async Task<List<WorkerWithDistance>> GetNearByWorkersAsync(double latitude,double longitude,double radiusInMeters,Guid serviceType)
        {
            var location = new Point(longitude, latitude)
            {
                SRID = 4326
            };

            var result = await dbContext.Workers
        .Include(s => s.ServiceType)
        .Where(w => w.Location != null &&
                    w.ServiceTypeId == serviceType &&
                    w.Location.Point.IsWithinDistance(location, radiusInMeters))
        .Select(w => new
        {
            Worker = w,
            Distance = w.Location.Point.Distance(location) / 1000.0
        })
        .OrderBy(x => x.Distance)
        .ThenByDescending(x => x.Worker.AverageRating)
        .ToListAsync(); // ✅ still EF here

            // switch to memory AFTER query executes
            return result
                .Select(x => new WorkerWithDistance
                {
                    Worker = x.Worker,
                    DistanceInKm = x.Distance
                })
                .ToList(); // ✅ now normal LINQ
        }

        public async Task<List<WorkerWithDistance>> GetNearbyWorkersWithBookingsAsync(double latitude, double longitude, double radiusInMeters, CancellationToken cancellationToken)
        {
            var location = new Point(longitude, latitude)
            {
                SRID = 4326
            };

            var result = await dbContext.Workers
                .Include(s => s.ServiceType)
                .Include(s => s.Bookings)
                .Where(w => w.Location != null &&
                            w.Location.Point.IsWithinDistance(location, radiusInMeters))
                .Select(w => new
                {
                    Worker = w,
                    Distance = w.Location.Point.Distance(location) / 1000.0
                })
                .OrderBy(x => x.Distance)
                .ToListAsync(cancellationToken);

            return result
                .Select(x => new WorkerWithDistance
                {
                    Worker = x.Worker,
                    DistanceInKm = Math.Round(x.Distance, 2)
                })
                .ToList();
        }
        public async Task<List<WorkerWithDistance>> GetAllWorkersWithDistanceAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            var location = new Point(longitude, latitude)
            {
                SRID = 4326
            };

            var result = await dbContext.Workers
                .Include(s => s.ServiceType)
                .Include(s => s.Bookings)
                .Where(w => w.Location != null)
                .Select(w => new
                {
                    Worker = w,
                    Distance = w.Location.Point.Distance(location) / 1000.0
                })
                .OrderBy(x => x.Distance)
                .ToListAsync(cancellationToken);

            return result
                .Select(x => new WorkerWithDistance
                {
                    Worker = x.Worker,
                    DistanceInKm = Math.Round(x.Distance, 2)
                })
                .ToList();
        }

        public async Task<List<WorkerWithDistance>> GetAvailableWorkersWithDistanceAsync(double? latitude, double? longitude, double? radiusInMeters, Guid? serviceTypeId, DateTime dateOfService, CancellationToken cancellationToken)
        {
            dateOfService = DateTime.SpecifyKind(dateOfService.Date, DateTimeKind.Utc);

            var activeStatuses = new[] { BookingStatus.Pending, BookingStatus.AcceptedAwaitingPayment, BookingStatus.MarkAsPaid, BookingStatus.AwaitingClientStartConfirmation, BookingStatus.InProgress, BookingStatus.AwaitingClientConfirmation };

            var query = dbContext.Workers
                .Include(s => s.ServiceType)
                .Where(w => w.IsAvailable)
                .Where(w => !w.Bookings.Any(b =>
                    b.DateOfService.Date == dateOfService.Date &&
                    activeStatuses.Contains(b.BookingStatus)));

            if (serviceTypeId.HasValue)
                query = query.Where(w => w.ServiceTypeId == serviceTypeId.Value);

            var hasOrigin = latitude.HasValue && longitude.HasValue;

            if (!hasOrigin)
            {
                var workers = await query
                    .OrderByDescending(w => w.AverageRating)
                    .ThenBy(w => w.FullName.FirstName)
                    .ToListAsync(cancellationToken);

                return workers.Select(w => new WorkerWithDistance
                {
                    Worker = w,
                    DistanceInKm = 0
                }).ToList();
            }

            var location = new Point(longitude!.Value, latitude!.Value)
            {
                SRID = 4326
            };

            if (radiusInMeters.HasValue && radiusInMeters.Value > 0)
                query = query.Where(w => w.Location != null && w.Location.Point.IsWithinDistance(location, radiusInMeters.Value));

            var result = await query
                .Where(w => w.Location != null)
                .Select(w => new
                {
                    Worker = w,
                    Distance = w.Location.Point.Distance(location) / 1000.0
                })
                .OrderBy(x => x.Distance)
                .ThenByDescending(x => x.Worker.AverageRating)
                .ToListAsync(cancellationToken);

            return result
                .Select(x => new WorkerWithDistance
                {
                    Worker = x.Worker,
                    DistanceInKm = Math.Round(x.Distance, 2)
                })
                .ToList();
        }

        public async Task<IEnumerable<Worker>> GetAllWorkers(CancellationToken cancellationToken)
        {
            return await dbContext.Workers.Include(s => s.ServiceType).ToListAsync(cancellationToken);
        }

        public async Task<List<Worker>> GetAllWorkersWithBookingsAsync(CancellationToken cancellationToken)
        {
            return await dbContext.Workers
                .Include(s => s.ServiceType)
                .Include(s => s.Bookings)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateWorker(Worker worker, CancellationToken cancellationToken)
        {
            dbContext.Workers.Update(worker);
        }

        public async Task DeleteWorker(Worker worker, CancellationToken cancellationToken)
        {
            dbContext.Workers.Remove(worker);
        }
    }
}
