using Clean_Connect.Domain.Entities;
using Clean_Connect.Application.DTO;

namespace Clean_Connect.Application.Command.PaymentCommand;

public static class PaymentVerification
{
    public static void EnsureMatches(Payment payment, VerifyData verified)
    {
        if (!string.Equals(verified.Reference, payment.PaymentReference, StringComparison.Ordinal)
            || !string.Equals(verified.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)
            || verified.Amount / 100m != payment.Amount)
            throw new InvalidOperationException("Verified transaction does not match the stored payment.");
    }
}
