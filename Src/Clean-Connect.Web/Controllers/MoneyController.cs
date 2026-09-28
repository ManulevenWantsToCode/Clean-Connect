using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Query.MoneyQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    [Authorize]
    public class MoneyController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _repo;
        private readonly INotyfService _notyf;
        private readonly ILogger<MoneyController> _logger;

        public MoneyController(IMediator mediator, IUnitOfWork repo, INotyfService notyf, ILogger<MoneyController> logger)
        {
            _mediator = mediator;
            _repo = repo;
            _notyf = notyf;
            _logger = logger;
        }

        [HttpGet("Money-Review")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            try
            {
                var dto = await _mediator.Send(new GetMoneyReviewQuery(), ct);
                return View("Review", dto);
            }
            catch (KeyNotFoundException)
            {
                _notyf.Warning("Complete your profile first to access your money review.");
                if (User.IsInRole("Worker"))
                    return RedirectToAction("CreateWorkerProfile", "Worker");
                return RedirectToAction("CreateClientProfile", "Client");
            }
        }
    }
}