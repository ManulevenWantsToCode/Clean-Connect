namespace Clean_Connect.Web.Helpers
{
    public static class BookingStatusHelper
    {
        public static string BookingLabel(string raw)
        {
            return raw switch
            {
                "AcceptedAwaitingPayment" => "Accepted · Awaiting Payment",
                "MarkAsPaid" => "Paid · Scheduled",
                "AwaitingClientStartConfirmation" => "Awaiting Start Confirmation",
                "InProgress" => "In Progress",
                "AwaitingClientConfirmation" => "Awaiting Confirmation",
                "Completed" => "Completed",
                "Rejected" => "Rejected",
                "Cancelled" => "Cancelled",
                "Expired" => "Expired",
                _ => "Pending"
            };
        }

        public static string BookingTone(string raw)
        {
            return raw switch
            {
                "AcceptedAwaitingPayment" => "success",
                "MarkAsPaid" => "success",
                "AwaitingClientStartConfirmation" => "warning",
                "InProgress" => "info",
                "AwaitingClientConfirmation" => "warning",
                "Completed" => "success",
                "Rejected" => "danger",
                "Cancelled" => "danger",
                "Expired" => "danger",
                _ => "warning"
            };
        }

        public static string PaymentLabel(string raw)
        {
            return raw switch
            {
                "Successful" => "Paid",
                "Refunded" => "Refunded",
                "Canceled" => "Cancelled",
                "Reversed" => "Reversed",
                "Abandoned" => "Abandoned",
                "Failed" => "Failed",
                _ => "Pending"
            };
        }

        public static string PaymentTone(string raw)
        {
            return raw switch
            {
                "Successful" => "success",
                "Refunded" => "warning",
                "Canceled" => "danger",
                "Reversed" => "danger",
                "Abandoned" => "danger",
                "Failed" => "danger",
                _ => "warning"
            };
        }

        public static int FlowStep(string bookingStatus)
        {
            return bookingStatus switch
            {
                "Pending" => 1,
                "AcceptedAwaitingPayment" => 2,
                "MarkAsPaid" => 3,
                "AwaitingClientStartConfirmation" => 3,
                "InProgress" => 4,
                "AwaitingClientConfirmation" => 4,
                "Completed" => 5,
                _ => 0
            };
        }
    }
}
