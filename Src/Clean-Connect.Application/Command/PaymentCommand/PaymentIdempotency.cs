using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Clean_Connect.Application.Command.PaymentCommand;

public static class PaymentIdempotency
{
    public static string ComputeRequestHash(Guid bookingId, decimal amount, string email, string method, string currency)
    {
        var canonical = string.Join("\n", bookingId.ToString("N"), amount.ToString("0.00", CultureInfo.InvariantCulture),
            email.Trim().ToLowerInvariant(), method.Trim().ToLowerInvariant(), currency.Trim().ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static void EnsureSameRequest(Payment existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            throw new PaymentInitializationConflictException("This idempotency key was already used with different payment details.");
    }
}
