using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Utilities;

namespace Clean_Connect.Domain.Entities
{
    public class Escrow : BaseEntity
    {
        private Escrow() { }

        private Escrow(Guid bookingId, Guid paymentId, Guid workerId, decimal amount, decimal commissionRate, decimal commissionAmount, string? createdBy = null)
        {
            ValidateId(bookingId, nameof(bookingId));
            ValidateId(paymentId, nameof(paymentId));
            ValidateId(workerId, nameof(workerId));
            ValidateAmount(amount);
            ValidateCommission(amount, commissionRate, commissionAmount);

            BookingId = bookingId;
            PaymentId = paymentId;
            WorkerId = workerId;
            Amount = amount;
            CommissionRate = commissionRate;
            CommissionAmount = commissionAmount;
            Status = EscrowStatus.Held;
            UpdateMetadata(createdBy);
        }

        public Guid BookingId { get; private set; }
        public Booking Booking { get; private set; } = default!;
        public Guid PaymentId { get; private set; }
        public Payment Payment { get; private set; } = default!;
        public Guid WorkerId { get; private set; }
        public Worker Worker { get; private set; } = default!;
        public decimal Amount { get; private set; }
        public decimal CommissionRate { get; private set; }
        public decimal CommissionAmount { get; private set; }
        public EscrowStatus Status { get; private set; }
        public DateTime? DateReleased { get; private set; }
        public string? PaystackTransferCode { get; private set; }
        public DateTime? DatePaidOut { get; private set; }

        public static Escrow Create(Guid bookingId, Guid paymentId, Guid workerId, decimal amount, decimal commissionRate, decimal commissionAmount, string? createdBy = null)
        {
            return new Escrow(bookingId, paymentId, workerId, amount, commissionRate, commissionAmount, createdBy);
        }

        public decimal WorkerShare => Amount - CommissionAmount;

        public void Release(string? modifiedBy = null)
        {
            if (Status == EscrowStatus.Released)
                throw new InvalidOperationException("Escrow has already been released.");

            if (Status != EscrowStatus.Held)
                throw new InvalidOperationException($"Escrow cannot be released from {Status} status.");

            Status = EscrowStatus.Released;
            DateReleased = DateTime.UtcNow;
            UpdateMetadata(modifiedBy);
        }

        public void MarkPaidOut(string paystackTransferCode, string? modifiedBy = null)
        {
            if (Status == EscrowStatus.PaidOut)
                throw new InvalidOperationException("Escrow has already been paid out.");

            if (Status != EscrowStatus.Held && Status != EscrowStatus.Released)
                throw new InvalidOperationException($"Escrow cannot be paid out from {Status} status.");

            if (string.IsNullOrWhiteSpace(paystackTransferCode))
                throw new ArgumentException("Paystack transfer code is required.", nameof(paystackTransferCode));

            Status = EscrowStatus.PaidOut;
            PaystackTransferCode = paystackTransferCode.Trim();
            DatePaidOut = DateTime.UtcNow;
            UpdateMetadata(modifiedBy);
        }

        public void MarkAsRefunded(string? modifiedBy = null)
        {
            if (Status == EscrowStatus.Refunded)
                return;

            if (Status != EscrowStatus.Held)
                throw new InvalidOperationException($"Escrow cannot be refunded from {Status} status.");

            Status = EscrowStatus.Refunded;
            UpdateMetadata(modifiedBy);
        }

        private static void ValidateId(Guid id, string paramName)
        {
            if (id == Guid.Empty)
                throw new ArgumentException($"{paramName} cannot be empty.", paramName);
        }

        private static void ValidateAmount(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        }

        private static void ValidateCommission(decimal amount, decimal commissionRate, decimal commissionAmount)
        {
            if (commissionRate < 0 || commissionRate > 100)
                throw new ArgumentException("Commission rate must be between 0 and 100.", nameof(commissionRate));

            if (commissionAmount < 0)
                throw new ArgumentException("Commission amount cannot be negative.", nameof(commissionAmount));

            if (commissionAmount > amount)
                throw new ArgumentException("Commission amount cannot exceed the escrow amount.", nameof(commissionAmount));
        }
    }
}
