using Clean_Connect.Application.DTO;
using Clean_Connect.Application.DTO.Pagination;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Helper;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories;

public sealed class PagedReadRepository(ApplicationDbContext db) : IPagedReadRepository
{
    public Task<PagedResponse<BookingDto>> GetClientBookingsAsync(Guid clientId, BookingPageRequest request, CancellationToken cancellationToken) =>
        GetBookingPageAsync(request, clientId, null, cancellationToken);

    public Task<PagedResponse<BookingDto>> GetWorkerBookingsAsync(Guid workerId, BookingPageRequest request, CancellationToken cancellationToken) =>
        GetBookingPageAsync(request, null, workerId, cancellationToken);

    public Task<PagedResponse<BookingDto>> GetAdminBookingsAsync(BookingPageRequest request, CancellationToken cancellationToken) =>
        GetBookingPageAsync(request, null, null, cancellationToken);

    private async Task<PagedResponse<BookingDto>> GetBookingPageAsync(
        BookingPageRequest request, Guid? clientId, Guid? workerId, CancellationToken cancellationToken)
    {
        IQueryable<Booking> query = db.Bookings.AsNoTracking().Where(x => !x.IsDeleted);
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId.Value);
        if (workerId.HasValue) query = query.Where(x => x.WorkerId == workerId.Value);
        if (request.ClientId.HasValue) query = query.Where(x => x.ClientId == request.ClientId.Value);
        if (request.WorkerId.HasValue) query = query.Where(x => x.WorkerId == request.WorkerId.Value);
        if (request.ServiceTypeId.HasValue) query = query.Where(x => x.ServiceTypeId == request.ServiceTypeId.Value);
        if (request.Status.HasValue) query = query.Where(x => x.BookingStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(x => x.ServiceType.Name.Contains(term)
                || x.Client.FullName.FirstName.Contains(term) || x.Client.FullName.LastName.Contains(term)
                || x.Worker.FullName.FirstName.Contains(term) || x.Worker.FullName.LastName.Contains(term));
        }
        if (request.FromDate.HasValue)
        {
            var from = NormalizeUtc(request.FromDate.Value);
            query = query.Where(x => x.DateOfService >= from);
        }
        if (request.ToDate.HasValue)
        {
            var to = InclusiveDateEnd(request.ToDate.Value);
            query = query.Where(x => x.DateOfService <= to);
        }

        var descending = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Booking> ordered = request.SortBy.ToLowerInvariant() switch
        {
            "createdat" => descending ? query.OrderByDescending(x => x.DateCreated) : query.OrderBy(x => x.DateCreated),
            "amount" => descending ? query.OrderByDescending(x => x.Amount) : query.OrderBy(x => x.Amount),
            "status" => descending ? query.OrderByDescending(x => x.BookingStatus) : query.OrderBy(x => x.BookingStatus),
            _ => descending ? query.OrderByDescending(x => x.DateOfService) : query.OrderBy(x => x.DateOfService)
        };

        var result = await ordered.ThenBy(x => x.Id)
            .Include(x => x.Client)
            .Include(x => x.Worker)
            .Include(x => x.ServiceType)
            .Include(x => x.Ratings)
            .ToPagedResponseAsync(request, cancellationToken);

        return result.Map(ToBookingDto);
    }

    public async Task<PagedResponse<ClientDto>> GetClientsAsync(ClientPageRequest request, CancellationToken cancellationToken)
    {
        IQueryable<Client> query = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.State))
        {
            var state = request.State.Trim();
            query = query.Where(x => x.State == state);
        }
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(x => x.FullName.FirstName.Contains(term)
                || x.FullName.LastName.Contains(term) || x.Email.Value.Contains(term));
        }

        var descending = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = request.SortBy.Equals("name", StringComparison.OrdinalIgnoreCase)
            ? (descending ? query.OrderByDescending(x => x.FullName.LastName).ThenByDescending(x => x.FullName.FirstName)
                : query.OrderBy(x => x.FullName.LastName).ThenBy(x => x.FullName.FirstName))
            : (descending ? query.OrderByDescending(x => x.DateCreated) : query.OrderBy(x => x.DateCreated));
        var page = await ordered.ThenBy(x => x.Id).ToPagedResponseAsync(request, cancellationToken);
        return page.Map(x => new ClientDto
        {
            Name = x.FullName.ToString(), Email = x.Email.ToString(), Address = x.Address.ToString(),
            Age = x.Age, Contact = x.PhoneNumber.ToString(), DateOfBirth = x.DateOfBirth,
            Gender = x.Gender.ToString(), State = x.State, ReferralCode = x.ReferralCode
        });
    }

    public async Task<PagedResponse<WorkerDto>> GetWorkersAsync(WorkerPageRequest request, CancellationToken cancellationToken)
    {
        IQueryable<Worker> query = db.Workers.AsNoTracking().Include(x => x.ServiceType);
        if (request.ServiceTypeId.HasValue) query = query.Where(x => x.ServiceTypeId == request.ServiceTypeId.Value);
        if (request.IsAvailable.HasValue) query = query.Where(x => x.IsAvailable == request.IsAvailable.Value);
        if (!string.IsNullOrWhiteSpace(request.State))
        {
            var state = request.State.Trim();
            query = query.Where(x => x.State == state);
        }
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(x => x.FullName.FirstName.Contains(term)
                || x.FullName.LastName.Contains(term) || x.Email.Value.Contains(term));
        }

        var descending = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Worker> ordered = request.SortBy.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(x => x.FullName.LastName).ThenByDescending(x => x.FullName.FirstName)
                : query.OrderBy(x => x.FullName.LastName).ThenBy(x => x.FullName.FirstName),
            "hourlyrate" => descending ? query.OrderByDescending(x => x.HourlyRate) : query.OrderBy(x => x.HourlyRate),
            _ => descending ? query.OrderByDescending(x => x.AverageRating) : query.OrderBy(x => x.AverageRating)
        };
        var page = await ordered.ThenBy(x => x.Id).ToPagedResponseAsync(request, cancellationToken);
        return page.Map(x => new WorkerDto
        {
            Name = x.FullName.ToString(), Email = x.Email.ToString(), Address = x.Address.ToString(),
            Contact = x.Contact.ToString(), Gender = x.Gender.ToString(), ServiceType = x.ServiceType.Name,
            Rating = x.AverageRating, Age = x.Age, Location = x.Location?.ToString() ?? string.Empty,
            DateOfBirth = x.DateOfBirth, State = x.State
        });
    }

    public async Task<PagedResponse<NearbyWorkerDto>> GetNearbyWorkersAsync(
        double latitude, double longitude, double radiusInMeters, Guid serviceTypeId,
        PaginationRequest request, CancellationToken cancellationToken)
    {
        var origin = new NetTopologySuite.Geometries.Point(longitude, latitude) { SRID = 4326 };
        var query = db.Workers.AsNoTracking()
            .Include(x => x.ServiceType)
            .Where(x => !x.IsSuspended && x.IsAvailable && x.ServiceTypeId == serviceTypeId
                && x.Location.Point.IsWithinDistance(origin, radiusInMeters))
            .Select(x => new WorkerWithDistance
            {
                Worker = x,
                DistanceInKm = x.Location.Point.Distance(origin) / 1000d
            })
            .OrderBy(x => x.DistanceInKm)
            .ThenByDescending(x => x.Worker.AverageRating)
            .ThenBy(x => x.Worker.Id);

        var page = await query.ToPagedResponseAsync(request, cancellationToken);
        return page.Map(x => new NearbyWorkerDto
        {
            WorkerId = x.Worker.Id, Name = x.Worker.FullName.ToString(),
            Rating = x.Worker.AverageRating, DistanceInKm = Math.Round(x.DistanceInKm, 2),
            ServiceType = x.Worker.ServiceType.Name, State = x.Worker.State, HourlyRate = x.Worker.HourlyRate
        });
    }

    public Task<PagedResponse<PaymentDto>> GetPaymentsAsync(PaymentPageRequest request, CancellationToken cancellationToken) =>
        GetPaymentPageAsync(db.Payments.AsNoTracking(), request, cancellationToken);

    public Task<PagedResponse<PaymentDto>> GetBookingPaymentsAsync(Guid bookingId, PaymentPageRequest request, CancellationToken cancellationToken) =>
        GetPaymentPageAsync(db.Payments.AsNoTracking().Where(x => x.BookingId == bookingId), request, cancellationToken);

    private static async Task<PagedResponse<PaymentDto>> GetPaymentPageAsync(
        IQueryable<Payment> query, PaymentPageRequest request, CancellationToken cancellationToken)
    {
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (request.BookingId.HasValue) query = query.Where(x => x.BookingId == request.BookingId.Value);
        if (request.FromDate.HasValue)
        {
            var from = NormalizeUtc(request.FromDate.Value);
            query = query.Where(x => x.DateCreated >= from);
        }
        if (request.ToDate.HasValue)
        {
            var to = InclusiveDateEnd(request.ToDate.Value);
            query = query.Where(x => x.DateCreated <= to);
        }

        var descending = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Payment> ordered = request.SortBy.ToLowerInvariant() switch
        {
            "amount" => descending ? query.OrderByDescending(x => x.Amount) : query.OrderBy(x => x.Amount),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            _ => descending ? query.OrderByDescending(x => x.DateCreated) : query.OrderBy(x => x.DateCreated)
        };
        var page = await ordered.ThenBy(x => x.Id).ToPagedResponseAsync(request, cancellationToken);
        return page.Map(x => new PaymentDto
        {
            Id = x.Id, BookingId = x.BookingId, Amount = x.Amount, PaymentMethod = x.PaymentMethod,
            Status = x.Status, FailureReason = x.FailureReason, PaymentReference = x.PaymentReference,
            Provider = x.Provider, TransactionId = x.TransactionId, CreatedAt = x.DateCreated, UpdatedAt = x.DateModified
        });
    }

    public async Task<PagedResponse<CouponDto>> GetCouponsAsync(CouponPageRequest request, CancellationToken cancellationToken)
    {
        IQueryable<Coupon> query = db.Coupons.AsNoTracking();
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        var descending = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Coupon> ordered = request.SortBy.ToLowerInvariant() switch
        {
            "code" => descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            "usage" => descending ? query.OrderByDescending(x => x.UsedCount) : query.OrderBy(x => x.UsedCount),
            _ => descending ? query.OrderByDescending(x => x.ExpirationDate) : query.OrderBy(x => x.ExpirationDate)
        };
        var page = await ordered.ThenBy(x => x.Id).ToPagedResponseAsync(request, cancellationToken);
        return page.Map(x => new CouponDto
        {
            Id = x.Id, Code = x.Code, DiscountPercentage = x.DiscountPercentage,
            ExpirationDate = x.ExpirationDate, UsageLimit = x.UsageLimit, UsedCount = x.UsedCount,
            IsActive = x.IsActive, IsValid = x.IsValid()
        });
    }

    private static BookingDto ToBookingDto(Booking x) => new()
    {
        Id = x.Id, ServiceName = x.ServiceType?.Name ?? string.Empty, ClientName = x.Client?.FullName?.ToString() ?? string.Empty,
        WorkersName = x.Worker?.FullName?.ToString() ?? string.Empty, BookingDate = x.DateOfBooking,
        DateOfService = x.DateOfService, StartTime = x.StartTime, EndTime = x.EndTime,
        TimeRange = x.TimeRange.ToString(), Amount = x.Amount, HourlyRate = x.HourlyRate,
        TotalAmount = x.TotalAmount, Address = x.Address?.ToString() ?? string.Empty, PaymentStatus = x.PaymentStatus.ToString(),
        BookingStatus = x.BookingStatus.ToString(), RejectionReason = x.RejectionReason,
        Rating = x.Ratings?.RatingValue ?? 0, HasRating = x.Ratings is not null
    };

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime InclusiveDateEnd(DateTime value)
    {
        var normalized = NormalizeUtc(value);
        return normalized.TimeOfDay == TimeSpan.Zero
            ? normalized.Date.AddDays(1).AddTicks(-1)
            : normalized;
    }
}

internal static class PaginationQueryExtensions
{
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query, PaginationRequest request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > PaginationRequest.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(request), "Page and pageSize are outside the supported range.");

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);
        var items = request.Offset > int.MaxValue
            ? new List<T>()
            : await query.Skip((int)request.Offset).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, request.Page, request.PageSize, totalCount, totalPages,
            request.Page > 1, request.Page < totalPages);
    }
}
