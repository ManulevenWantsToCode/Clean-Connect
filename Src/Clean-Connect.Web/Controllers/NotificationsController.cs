using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly IUnitOfWork _repo;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(IUnitOfWork repo, ILogger<NotificationsController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet("Notifications")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var client = await _repo.Clients.GetByEmail(email, ct);
                if (client != null)
                {
                    var notifications = await _repo.Notifications.GetByTargetAsync(client.Id, NotificationAudience.Client, ct);
                    return View(ToPage(notifications, "Client"));
                }

                var worker = await _repo.Workers.GetByEmail(email, ct);
                if (worker != null)
                {
                    var notifications = await _repo.Notifications.GetByTargetAsync(worker.Id, NotificationAudience.Worker, ct);
                    return View(ToPage(notifications, "Worker"));
                }

                return View(new NotificationPageViewModel
                {
                    Audience = "User",
                    Items = new List<NotificationItemViewModel>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load notifications for {Email}", email);
                return View(new NotificationPageViewModel { Audience = "User" });
            }
        }

        [HttpGet("Notifications/UnreadCount")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { count = 0 });
            }

            try
            {
                var client = await _repo.Clients.GetByEmail(email, ct);
                if (client != null)
                {
                    var count = await _repo.Notifications.GetUnreadCountAsync(client.Id, NotificationAudience.Client, ct);
                    return Json(new { count });
                }

                var worker = await _repo.Workers.GetByEmail(email, ct);
                if (worker != null)
                {
                    var count = await _repo.Notifications.GetUnreadCountAsync(worker.Id, NotificationAudience.Worker, ct);
                    return Json(new { count });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load unread count for {Email}", email);
            }

            return Json(new { count = 0 });
        }

        [HttpPost("Notifications/MarkAsRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            try
            {
                await _repo.Notifications.MarkAsReadAsync(id, ct);
                await _repo.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark notification {Id} as read", id);
            }

            return Ok();
        }

        [HttpPost("Notifications/MarkAllAsRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            try
            {
                var client = await _repo.Clients.GetByEmail(email, ct);
                if (client != null)
                {
                    await _repo.Notifications.MarkAllAsReadAsync(client.Id, NotificationAudience.Client, ct);
                    await _repo.SaveChangesAsync(ct);
                    return Ok();
                }

                var worker = await _repo.Workers.GetByEmail(email, ct);
                if (worker != null)
                {
                    await _repo.Notifications.MarkAllAsReadAsync(worker.Id, NotificationAudience.Worker, ct);
                    await _repo.SaveChangesAsync(ct);
                    return Ok();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark all notifications as read for {Email}", email);
            }

            return Ok();
        }

        private static NotificationPageViewModel ToPage(IReadOnlyCollection<Domain.Entities.Notification> notifications, string audience)
        {
            var items = notifications.Select(n => new NotificationItemViewModel
            {
                NotificationId = n.Id,
                IsRead = n.IsRead,
                BookingId = n.BookingId,
                Title = n.Title,
                Message = n.Message,
                Status = n.Status,
                Tone = n.Tone,
                Icon = n.Icon,
                ActionText = n.ActionText,
                ActionUrl = NormalizeActionUrl(n.ActionUrl),
                CreatedAt = n.DateCreated,
                NeedsAttention = n.NeedsAttention && !n.IsRead
            }).ToList();

            return new NotificationPageViewModel
            {
                Audience = audience,
                NeedsAttentionCount = items.Count(x => x.NeedsAttention),
                Items = items
            };
        }

        private static string NormalizeActionUrl(string actionUrl)
        {
            if (string.IsNullOrWhiteSpace(actionUrl))
            {
                return actionUrl;
            }

            if (actionUrl.StartsWith("/Booking/WorkerDetails", StringComparison.OrdinalIgnoreCase))
            {
                var id = ExtractQueryValue(actionUrl, "bookingId");
                return string.IsNullOrEmpty(id) ? actionUrl : $"/Worker-Booking-Details/{id}";
            }

            if (actionUrl.StartsWith("/Booking/Details", StringComparison.OrdinalIgnoreCase))
            {
                var id = ExtractQueryValue(actionUrl, "bookingId");
                return string.IsNullOrEmpty(id) ? actionUrl : $"/Client-Booking-Details/{id}";
            }

            return actionUrl;
        }

        private static string? ExtractQueryValue(string url, string key)
        {
            try
            {
                var index = url.IndexOf('?');
                if (index < 0)
                {
                    return null;
                }

                var query = url[(index + 1)..];
                foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(parts[1]);
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}