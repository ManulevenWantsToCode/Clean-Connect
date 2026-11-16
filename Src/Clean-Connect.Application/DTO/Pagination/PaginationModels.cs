using System.ComponentModel.DataAnnotations;
using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Application.DTO.Pagination;

public class PaginationRequest
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaximumPageSize)]
    public int PageSize { get; set; } = DefaultPageSize;

    public long Offset => ((long)Page - 1) * PageSize;
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage)
{
    public PagedResponse<TNext> Map<TNext>(Func<T, TNext> map) => new(
        Items.Select(map).ToArray(), Page, PageSize, TotalCount, TotalPages, HasPreviousPage, HasNextPage);
}

public sealed class BookingPageRequest : PaginationRequest
{
    public BookingStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ServiceTypeId { get; set; }
    public Guid? WorkerId { get; set; }
    public Guid? ClientId { get; set; }
    [MaxLength(100)]
    public string? SearchTerm { get; set; }
    [RegularExpression("^(serviceDate|createdAt|amount|status)$", ErrorMessage = "Unsupported booking sort field.")]
    public string SortBy { get; set; } = "serviceDate";
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortDirection must be asc or desc.")]
    public string SortDirection { get; set; } = "desc";
}

public sealed class PaymentPageRequest : PaginationRequest
{
    public PaymentStatus? Status { get; set; }
    public Guid? BookingId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [RegularExpression("^(createdAt|amount|status)$", ErrorMessage = "Unsupported payment sort field.")]
    public string SortBy { get; set; } = "createdAt";
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortDirection must be asc or desc.")]
    public string SortDirection { get; set; } = "desc";
}

public sealed class WorkerPageRequest : PaginationRequest
{
    public Guid? ServiceTypeId { get; set; }
    [MaxLength(100)]
    public string? State { get; set; }
    [MaxLength(100)]
    public string? SearchTerm { get; set; }
    public bool? IsAvailable { get; set; }
    [RegularExpression("^(name|rating|hourlyRate)$", ErrorMessage = "Unsupported worker sort field.")]
    public string SortBy { get; set; } = "rating";
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortDirection must be asc or desc.")]
    public string SortDirection { get; set; } = "desc";
}

public sealed class ClientPageRequest : PaginationRequest
{
    [MaxLength(100)]
    public string? State { get; set; }
    [MaxLength(100)]
    public string? SearchTerm { get; set; }
    [RegularExpression("^(name|createdAt)$", ErrorMessage = "Unsupported client sort field.")]
    public string SortBy { get; set; } = "createdAt";
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortDirection must be asc or desc.")]
    public string SortDirection { get; set; } = "desc";
}

public sealed class CouponPageRequest : PaginationRequest
{
    public bool? IsActive { get; set; }
    [RegularExpression("^(code|expirationDate|usage)$", ErrorMessage = "Unsupported coupon sort field.")]
    public string SortBy { get; set; } = "expirationDate";
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortDirection must be asc or desc.")]
    public string SortDirection { get; set; } = "desc";
}
