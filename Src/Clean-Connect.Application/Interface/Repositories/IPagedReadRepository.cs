using Clean_Connect.Application.DTO;
using Clean_Connect.Application.DTO.Pagination;

namespace Clean_Connect.Application.Interface.Repositories;

public interface IPagedReadRepository
{
    Task<PagedResponse<BookingDto>> GetClientBookingsAsync(Guid clientId, BookingPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<BookingDto>> GetWorkerBookingsAsync(Guid workerId, BookingPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<BookingDto>> GetAdminBookingsAsync(BookingPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<ClientDto>> GetClientsAsync(ClientPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<WorkerDto>> GetWorkersAsync(WorkerPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<NearbyWorkerDto>> GetNearbyWorkersAsync(double latitude, double longitude, double radiusInMeters, Guid serviceTypeId, PaginationRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<PaymentDto>> GetPaymentsAsync(PaymentPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<PaymentDto>> GetBookingPaymentsAsync(Guid bookingId, PaymentPageRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<CouponDto>> GetCouponsAsync(CouponPageRequest request, CancellationToken cancellationToken);
}
