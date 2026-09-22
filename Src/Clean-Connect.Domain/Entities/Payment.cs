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

        public string PaymentReference { get; private set; }

        public string Provider { get; private set; } = "paystack";
        public string? AuthorizationCode { get; private set; } = default!;
        public string? TransactionId { get; private set; } = default!;

        public static Payment Create(Guid bookingId, decimal amount, string paymentReference, string paymentMethod, string transactionId, DateTime createdAt, string? createdBy = null)
        {

            ValidateBookingId(bookingId);
            ValidatePaymentReference(paymentReference);
            ValidateAmount(amount);

            

            var payment = new Payment(bookingId, amount, PaymentStatus.Pending, paymentMethod, createdAt, createdBy);

            payment.PaymentReference = paymentReference;

            payment.TransactionId = transactionId ?? string.Empty;
            payment.AddDomainEvent(new PaymentCreatedEvent(payment.Id));
            return payment;
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

            AddDomainEvent(new PaymentAbandonedEvent(Id, reason));
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
            if (Status != PaymentStatus.Successful && Status != PaymentStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot refund a payment in {Status} status.");
            }

            Status = PaymentStatus.Refunded;
            FailureReason = reason;
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

