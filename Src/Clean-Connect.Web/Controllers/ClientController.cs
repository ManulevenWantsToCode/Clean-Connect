using AspNetCoreHero.ToastNotification.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    public class ClientController : Controller
    {
        private readonly ILogger<ClientController> _logger;
        private readonly IMediator _mediator;
        private readonly INotyfService _notyf;

        public ClientController(ILogger<ClientController> logger, IMediator mediator, INotyfService notyf)
        {
            _logger = logger;
            _mediator = mediator;
            _notyf = notyf;
        }

        [HttpGet("Create-Account")]
        public IActionResult CreateAccount()
        {
            return View();
        }
    }
}
