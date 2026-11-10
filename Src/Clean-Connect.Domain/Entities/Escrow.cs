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
        public string? PaystackTransferReference { get; private set; }
        public PayoutTransferStatus TransferStatus { get; private set; } = PayoutTransferStatus.NotStarted;
        public int PayoutAttemptCount { get; private set; }
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
            {
                if (!string.IsNullOrWhiteSpace(PaystackTransferCode)
                    && string.Equals(PaystackTransferCode, paystackTransferCode, StringComparison.Ordinal))
                    return;
                throw new InvalidOperationException("Escrow has already been paid out.");
            }

            if (Status != EscrowStatus.Held && Status != EscrowStatus.Released)
                throw new InvalidOperationException($"Escrow cannot be paid out from {Status} status.");

            if (string.IsNullOrWhiteSpace(paystackTransferCode))
                throw new ArgumentException("Paystack transfer code is required.", nameof(paystackTransferCode));

            Status = EscrowStatus.PaidOut;
            PaystackTransferCode = paystackTransferCode.Trim();
            TransferStatus = PayoutTransferStatus.Succeeded;
            DatePaidOut = DateTime.UtcNow;
            UpdateMetadata(modifiedBy);
        }

        public string BeginPayoutAttempt(string? modifiedBy = null)
        {
            if (Status != EscrowStatus.Held)
                throw new InvalidOperationException($"Escrow cannot start a payout from {Status} status.");

            if (TransferStatus is PayoutTransferStatus.Pending or PayoutTransferStatus.OtpRequired)
                throw new InvalidOperationException($"Payout is already {TransferStatus}.");

            if (TransferStatus is PayoutTransferStatus.Failed or PayoutTransferStatus.Reversed or PayoutTransferStatus.NotStarted)
            {
                PayoutAttemptCount++;
                PaystackTransferReference = $"payout-{Id:N}-{PayoutAttemptCount}";
                PaystackTransferCode = null;
            }

            if (string.IsNullOrWhiteSpace(PaystackTransferReference))
                throw new InvalidOperationException("A transfer reference could not be created for this payout.");

            TransferStatus = PayoutTransferStatus.Initiating;
            UpdateMetadata(modifiedBy);
            return PaystackTransferReference;
        }

        public void RecordPayoutTransfer(string transferCode, string providerStatus, string? modifiedBy = null)
        {
            var normalizedStatus = providerStatus.Trim().ToLowerInvariant();
            if ((TransferStatus is PayoutTransferStatus.Failed or PayoutTransferStatus.Reversed)
                && (normalizedStatus is "success" or "pending" or "otp"))
                return;
            if (Status == EscrowStatus.PaidOut)
                return;
            if (Status != EscrowStatus.Held)
                throw new InvalidOperationException($"Escrow cannot record a payout from {Status} status.");

            if (!string.IsNullOrWhiteSpace(transferCode))
                PaystackTransferCode = transferCode.Trim();

            TransferStatus = normalizedStatus switch
            {
                "success" => PayoutTransferStatus.Succeeded,
                "pending" or "received" => PayoutTransferStatus.Pending,
                "otp" => PayoutTransferStatus.OtpRequired,
                "failed" or "abandoned" or "blocked" or "rejected" => PayoutTransferStatus.Failed,
                "reversed" => PayoutTransferStatus.Reversed,
                _ => PayoutTransferStatus.Unknown
            };

            if (TransferStatus == PayoutTransferStatus.Succeeded)
            {
                if (string.IsNullOrWhiteSpace(PaystackTransferCode))
                    throw new ArgumentException("Paystack transfer code is required for a successful payout.", nameof(transferCode));
                Status = EscrowStatus.PaidOut;
                DatePaidOut = DateTime.UtcNow;
            }
            UpdateMetadata(modifiedBy);
        }

        public void MarkPayoutRetryable(string providerStatus, string? modifiedBy = null)
        {
            var isReversal = providerStatus.Trim().Equals("reversed", StringComparison.OrdinalIgnoreCase);
            if (Status == EscrowStatus.PaidOut && !isReversal)
                return;
            if (Status != EscrowStatus.Held && !(Status == EscrowStatus.PaidOut && isReversal))
                throw new InvalidOperationException($"Escrow cannot update payout from {Status} status.");

            if (isReversal && Status == EscrowStatus.PaidOut)
            {
                Status = EscrowStatus.Held;
                DatePaidOut = null;
            }
            TransferStatus = isReversal ? PayoutTransferStatus.Reversed : PayoutTransferStatus.Failed;
            UpdateMetadata(modifiedBy);
        }

        public void MarkPayoutUnknown(string? modifiedBy = null)
        {
            if (Status == EscrowStatus.PaidOut)
                return;
            if (Status != EscrowStatus.Held)
                throw new InvalidOperationException($"Escrow cannot update payout from {Status} status.");
            if (TransferStatus is not (PayoutTransferStatus.Initiating or PayoutTransferStatus.Unknown))
                return;
            TransferStatus = PayoutTransferStatus.Unknown;
            UpdateMetadata(modifiedBy);
        }

        public void ReconcileLegacyPayout(string providerStatus, string? transferCode, string? modifiedBy = null)
        {
            if (Status != EscrowStatus.PaidOut || TransferStatus != PayoutTransferStatus.LegacyUnverified)
                throw new InvalidOperationException("Only a legacy payout awaiting verification can be reconciled this way.");

            if (!string.IsNullOrWhiteSpace(transferCode)
                && !string.Equals(PaystackTransferCode, transferCode, StringComparison.Ordinal))
                throw new InvalidOperationException("Paystack returned a different transfer code for this legacy payout.");

            switch (providerStatus.Trim().ToLowerInvariant())
            {
                case "success":
                    TransferStatus = PayoutTransferStatus.Succeeded;
                    break;
                case "pending":
                case "received":
                    Status = EscrowStatus.Held;
                    TransferStatus = PayoutTransferStatus.Pending;
                    DatePaidOut = null;
                    break;
                case "otp":
                    Status = EscrowStatus.Held;
                    TransferStatus = PayoutTransferStatus.OtpRequired;
                    DatePaidOut = null;
                    break;
                case "failed":
                case "abandoned":
                case "blocked":
                case "rejected":
                    Status = EscrowStatus.Held;
                    TransferStatus = PayoutTransferStatus.Failed;
                    DatePaidOut = null;
                    break;
                case "reversed":
                    Status = EscrowStatus.Held;
                    TransferStatus = PayoutTransferStatus.Reversed;
                    DatePaidOut = null;
                    break;
                default:
                    return;
            }

            UpdateMetadata(modifiedBy);
        }

        public void MarkAsRefunded(string? modifiedBy = null)
        {
            if (Status == EscrowStatus.Refunded)
                return;

            if (Status != EscrowStatus.Held)
                throw new InvalidOperationException($"Escrow cannot be refunded from {Status} status.");

            if (TransferStatus is PayoutTransferStatus.Initiating or PayoutTransferStatus.Pending
                or PayoutTransferStatus.OtpRequired or PayoutTransferStatus.Unknown)
                throw new InvalidOperationException("Escrow cannot be refunded while a transfer outcome is unresolved.");

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
