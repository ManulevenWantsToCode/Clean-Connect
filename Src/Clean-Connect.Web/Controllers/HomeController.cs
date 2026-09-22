using Clean_Connect.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Clean_Connect.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Client"))
                    return RedirectToAction("Dashboard", "Client");
                if (User.IsInRole("Worker"))
                    return RedirectToAction("Dashboard", "Worker");
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            var code = statusCode ?? (HttpContext.Response.StatusCode > 0 ? HttpContext.Response.StatusCode : 500);

            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = code
            };

            switch (code)
            {
                case 404:
                    model.Title = "Page not found";
                    model.Message = "The page you're looking for doesn't exist or has been moved. Check the address and try again.";
                    break;
                case 403:
                    model.Title = "Access denied";
                    model.Message = "You don't have permission to view this page. If you think this is a mistake, contact support.";
                    break;
                case 401:
                    model.Title = "Sign in required";
                    model.Message = "Please sign in to view this page.";
                    break;
                default:
                    model.Title = "Something went wrong";
                    model.Message = "An unexpected error occurred while processing your request. Please try again.";
                    break;
            }

            return View(model);
        }
    }
}
