using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Clean_Connect.Web.Services
{
    public class AdminSidebarCountsFilter : IAsyncActionFilter
    {
        private readonly IUnitOfWork _repo;
        private readonly ILogger<AdminSidebarCountsFilter> _logger;

        public AdminSidebarCountsFilter(IUnitOfWork repo, ILogger<AdminSidebarCountsFilter> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.IsInRole("Admin"))
            {
                try
                {
                    var workers = (await _repo.Workers.GetAllWorkers(context.HttpContext.RequestAborted)).ToList();
                    var bookings = (await _repo.Bookings.GetAllBookings(context.HttpContext.RequestAborted)).ToList();
                    var ratings = await _repo.Ratings.GetAllRatingsAsync(context.HttpContext.RequestAborted);

                    var viewData = (context.Controller as Controller)?.ViewData;
                    if (viewData != null)
                    {
                        // The domain has no pending-approval state. Show the actual
                        // registered worker total instead of mislabeling off-duty workers as pending.
                        viewData["SidebarWorkerCount"] = workers.Count(w => !w.IsDeleted);
                        viewData["SidebarNewBookings"] = bookings.Count(b => !b.IsDeleted && b.BookingStatus == BookingStatus.Pending);
                        viewData["SidebarFlaggedReviews"] = ratings.Count(r => !r.IsDeleted && r.RatingValue <= 2);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load admin sidebar counts.");
                }
            }

            await next();
        }
    }
}
