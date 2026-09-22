namespace Clean_Connect.Web.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public int StatusCode { get; set; } = 500;

        public string Title { get; set; } = "Something went wrong";

        public string Message { get; set; } = "An unexpected error occurred while processing your request. Please try again.";

        public string? ErrorCode { get; set; }
    }
}
