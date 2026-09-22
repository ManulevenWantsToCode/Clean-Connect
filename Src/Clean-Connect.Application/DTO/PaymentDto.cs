using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Application.DTO
{
    public record ClientPaymentRequest(Guid ClientId, string Email, string PaymentMethod, string? CreatedBy = null);

    public record PaymentInitializationResponse(
        Guid BookingId,
        decimal Amount,
        string PaymentReference,
        string CheckoutUrl);

    public record PaymentConfirmationResult(
        Guid BookingId,
        Guid PaymentId,
        decimal Amount,
        string PaymentReference,
        string? TransactionId,
        string Status,
        string? FailureReason);

    public record PaymentDto
    {
            public Guid Id { get; set; }

            public Guid BookingId { get; set; }

            public decimal Amount { get; set; }

            public string? PaymentMethod { get; set; }

            public PaymentStatus Status { get; set; }

            public string? FailureReason { get; set; }

            public string PaymentReference { get; set; } = default!;

            public string Provider { get; set; } = default!;

            public string? TransactionId { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime? UpdatedAt { get; set; }
        
    }
}
