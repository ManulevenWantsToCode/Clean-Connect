using Clean_Connect.Application.Interface.Services;

namespace Clean_Connect.Web.Services
{
    public interface IRealtimeNotificationService : IMessageRealtimeNotificationService
    {
        Task NotifyUserAsync(string? email, string title, string message, string type = "info", string? url = null);

        Task NotifyAdminAsync(string title, string message, string type = "info", string? url = null);
    }
}
