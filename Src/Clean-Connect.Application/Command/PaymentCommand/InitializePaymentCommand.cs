namespace Clean_Connect.Application.Command.PaymentCommand;

// Request shape retained for API compatibility. The controller replaces all
// financial and identity values with trusted server-side values before handling.
public sealed record InitializePaymentCommand(
    Guid BookingId,
    decimal Amount,
    string Email,
    string PaymentMethod,
    string? TransactionId,
    string? CreatedBy);
