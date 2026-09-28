namespace Clean_Connect.Web.Services
{
    public interface IRealtimeNotificationService
    {
        Task NotifyUserAsync(string? email, string title, string message, string type = "info", string? url = null);

        Task NotifyAdminAsync(string title, string message, string type = "info", string? url = null);
    }
}
