namespace Clean_Connect.Web.Models
{
    public class PaymentConfirmationViewModel
    {
        public bool Succeeded { get; set; }

        public string? Title { get; set; }

        public string? Message { get; set; }

        public Guid BookingId { get; set; }

        public string? ServiceName { get; set; }

        public string? WorkerName { get; set; }

        public decimal Amount { get; set; }

        public string? PaymentReference { get; set; }

        public string? TransactionId { get; set; }

        public string? FailureReason { get; set; }
    }
}