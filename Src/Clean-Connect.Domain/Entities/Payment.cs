using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Events;
using Clean_Connect.Domain.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Domain.Entities
{
    public class Payment : BaseEntity
    {
        private Payment() { }
        private Payment(Guid bookingId, decimal amount, PaymentStatus paymentstatus, string paymentMethod, DateTime createdAt, string? createdBy = null)
        {
            BookingId = bookingId;
            Amount = amount;
            Status = paymentstatus;
            PaymentMethod = paymentMethod ?? throw new ArgumentNullException(nameof(PaymentMethod));
            DateCreated = createdAt;

            UpdateMetadata(createdBy);
        }
        public Guid BookingId { get; private set; }
        public decimal Amount { get; private set; }
        public string? PaymentMethod { get; private set; } = default!;
        public PaymentStatus Status { get; private set; } = default!;
        public string? FailureReason { get; private set; } = default!;

        public string PaymentReference { get; private set; } = string.Empty;

        public string Provider { get; private set; } = "paystack";
        public string? AuthorizationCode { get; private set; } = default!;
        public string? TransactionId { get; private set; } = default!;
        public string? IdempotencyKey { get; private set; }
        public string? RequestHash { get; private set; }
        public string? AuthorizationUrl { get; private set; }
        public PaymentInitializationStatus InitializationStatus { get; private set; } = PaymentInitializationStatus.Pending;
        public DateTime? InitializationAttemptedAtUtc { get; private set; }
        public string? InitializationErrorCategory { get; private set; }
        public string Currency { get; private set; } = "NGN";
        public string RefundStatus { get; private set; } = "NotStarted";
        public string? RefundReference { get; private set; }
        public decimal RefundAmount { get; private set; }
        public decimal RefundedAmount { get; private set; }

        public static Payment Create(Guid bookingId, decimal amount, string paymentReference, string paymentMethod, string transactionId, DateTime createdAt, string? createdBy = null, string? idempotencyKey = null, string? requestHash = null, string currency = "NGN")
        {

            ValidateBookingId(bookingId);
            ValidatePaymentReference(paymentReference);
            ValidateAmount(amount);

            

            var payment = new Payment(bookingId, amount, PaymentStatus.Pending, paymentMethod, createdAt, createdBy);

            payment.PaymentReference = paymentReference;

            payment.TransactionId = transactionId ?? string.Empty;
            payment.IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
            payment.RequestHash = requestHash;
            payment.Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant();
            payment.AddDomainEvent(new PaymentCreatedEvent(payment.Id));
            return payment;
        }

        public void SetAuthorizationUrl(string authorizationUrl)
        {
            if (Status != PaymentStatus.Pending || string.IsNullOrWhiteSpace(authorizationUrl))
                throw new InvalidOperationException("A checkout URL can only be saved for a pending payment.");
            AuthorizationUrl = authorizationUrl;
            InitializationStatus = PaymentInitializationStatus.Initialized;
            InitializationErrorCategory = null;
        }

        public void MarkInitializationAttempted(DateTime attemptedAtUtc)
        {
            if (Status != PaymentStatus.Pending || InitializationStatus == PaymentInitializationStatus.Initialized)
                throw new InvalidOperationException("Only an unresolved pending payment can be initialized.");
            InitializationAttemptedAtUtc = attemptedAtUtc;
            InitializationStatus = PaymentInitializationStatus.Pending;
            InitializationErrorCategory = null;
        }

        public void RequireInitializationReconciliation(string safeErrorCategory)
        {
            if (Status != PaymentStatus.Pending || InitializationStatus == PaymentInitializationStatus.Initialized)
                return;
            InitializationStatus = PaymentInitializationStatus.ReconciliationRequired;
            InitializationErrorCategory = NormalizeSafeCategory(safeErrorCategory);
        }

        public void MarkInitializationFailed(string safeErrorCategory)
        {
            if (Status != PaymentStatus.Pending || InitializationStatus == PaymentInitializationStatus.Initialized)
                return;
            InitializationStatus = PaymentInitializationStatus.Failed;
            InitializationErrorCategory = NormalizeSafeCategory(safeErrorCategory);
        }

        private static string NormalizeSafeCategory(string category) =>
            string.IsNullOrWhiteSpace(category) ? "Unknown" : category[..Math.Min(category.Length, 64)];

        public void BeginRefund(string refundReference, decimal amount, string? modifiedBy = null)
        {
            if (Status != PaymentStatus.Successful)
                throw new InvalidOperationException($"Cannot refund a payment in {Status} status.");
            if (string.IsNullOrWhiteSpace(refundReference))
                throw new ArgumentException("A refund reference is required.", nameof(refundReference));
            if (amount <= 0 || decimal.Round(amount, 2) != amount || amount > Amount - RefundedAmount)
                throw new InvalidOperationException("Refund amount exceeds the remaining refundable balance.");
            if (RefundReference is not null && (RefundReference != refundReference.Trim() || RefundAmount != amount))
                throw new InvalidOperationException("A payment already has a refund attempt with different details.");
            if (RefundStatus is "Initiating" or "Pending" or "Processing" or "NeedsAttention")
                return;
            RefundReference = refundReference.Trim();
            RefundAmount = amount;
            RefundStatus = "Initiating";
            UpdateMetadata(modifiedBy);
        }

        public void MarkRefundPending(string providerStatus, string? modifiedBy = null)
        {
            if (string.IsNullOrWhiteSpace(RefundReference))
                throw new InvalidOperationException("A refund reference must be persisted before updating its status.");
            RefundStatus = string.IsNullOrWhiteSpace(providerStatus) ? "Pending" : providerStatus.Trim();
            UpdateMetadata(modifiedBy);
        }

        public void MarkAsPaid(string authorizationCode, string transactionId, string? modifiedBy = null)
        {
            ValidatePaymentStatusTransition(PaymentStatus.Successful);
            Status = PaymentStatus.Successful;
            TransactionId = transactionId;
            AuthorizationCode = authorizationCode;
            UpdateMetadata(modifiedBy);
        }

        public void MarkAsAbandoned(string? reason, string? modifiedBy = null)
        {
            ValidatePaymentStatusTransition(PaymentStatus.Abandoned);

            Status = PaymentStatus.Abandoned;
            FailureReason = reason;

            UpdateMetadata(modifiedBy);

            AddDomainEvent(new PaymentAbandonedEvent(Id, reason ?? string.Empty));
        }

        public void MarkAsFailed(string? failureReason, string? transactionId, string? modifiedBy = null)
        {
            ValidatePaymentStatusTransition(PaymentStatus.Failed);
            Status = PaymentStatus.Failed;
            FailureReason = failureReason;
            TransactionId = transactionId ?? string.Empty;
            UpdateMetadata(modifiedBy);
        }

        public void MarkAsRefunded(string? reason, string? modifiedBy = null)
        {
            if (Status == PaymentStatus.Refunded && RefundStatus == "Processed")
                return;
            if (Status != PaymentStatus.Successful)
            {
                throw new InvalidOperationException($"Cannot refund a payment in {Status} status.");
            }
            if (RefundAmount <= 0 || RefundAmount > Amount - RefundedAmount)
                throw new InvalidOperationException("Confirmed refund exceeds the remaining refundable balance.");

            Status = PaymentStatus.Refunded;
            RefundedAmount += RefundAmount;
            FailureReason = reason;
            RefundStatus = "Processed";
            UpdateMetadata(modifiedBy);
        }

        public static void ValidateBookingId(Guid bookingId)
        {
            if (bookingId == Guid.Empty)
            {
                throw new ArgumentException("BookingId cannot be empty.", nameof(bookingId));
            }
        }

        public static void ValidateAmount(decimal amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
            }
        }

        public static void ValidatePaymentReference(string paymentReference)
        {
            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                throw new ArgumentException("Payment reference cannot be null or empty.", nameof(paymentReference));
            }
        }
        public void ValidatePaymentStatusTransition(PaymentStatus newStatus)
        {
            if (Status == PaymentStatus.Successful)
                throw new InvalidOperationException("Payment already completed");

            if (Status == PaymentStatus.Refunded || Status == PaymentStatus.Reversed)
                throw new InvalidOperationException("Cannot change refunded/reversed payment");

            if (Status == PaymentStatus.Abandoned || Status == PaymentStatus.Failed)
                throw new InvalidOperationException("Cannot move from failed/abandoned state");

            // Optional strict rule:
            if (Status == PaymentStatus.Pending && newStatus != PaymentStatus.Successful && newStatus != PaymentStatus.Failed)
                throw new InvalidOperationException($"Invalid transition from {Status} to {newStatus}");
        }

    }


}

