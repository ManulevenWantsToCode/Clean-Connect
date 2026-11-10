using Clean_Connect.Application.Command.Auth;
using Clean_Connect.Web.Models;
using AspNetCoreHero.ToastNotification.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net;

namespace Clean_Connect.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly INotyfService _notyf;

        public HomeController(ILogger<HomeController> logger, IMediator mediator, IConfiguration configuration, INotyfService notyf)
        {
            _logger = logger;
            _mediator = mediator;
            _configuration = configuration;
            _notyf = notyf;
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

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View(new ContactMessageViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactMessageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var body = string.Join("<br/>",
                $"<strong>Name:</strong> {WebUtility.HtmlEncode(model.Name)}",
                $"<strong>Email:</strong> {WebUtility.HtmlEncode(model.Email)}",
                $"<strong>Phone:</strong> {WebUtility.HtmlEncode(model.Phone ?? "-")}",
                $"<strong>Topic:</strong> {WebUtility.HtmlEncode(model.Topic)}",
                "",
                "<strong>Message:</strong>",
                WebUtility.HtmlEncode(model.Message)
            );

            try
            {
                var supportEmail = _configuration["Email:Smtp:From"] ?? "support@cleanconnect.com";
                await _mediator.Send(new EmailSenderCommand(
                    supportEmail,
                    body,
                    $"Clean Connect contact form: {model.Topic}",
                    model.Email,
                    model.Name));

                _notyf.Success("Message sent. We will reply to the email you provided.");
                return RedirectToAction(nameof(Contact));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver contact message from {Email}", model.Email);
                _notyf.Error("Something went wrong sending your message. Please try again.");
                return View(model);
            }
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
