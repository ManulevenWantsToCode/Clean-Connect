using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.ServiceTypeCommands;
using Clean_Connect.Application.Query.ServiceTypeQuery;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    public class ServiceTypeController : Controller
    {
        private readonly ILogger<ServiceTypeController> _logger;
        private readonly IMediator _mediator;
        private readonly INotyfService _notyf;

        public ServiceTypeController(ILogger<ServiceTypeController> logger, IMediator mediator, INotyfService notyf)
        {
            _logger = logger;
            _mediator = mediator;
            _notyf = notyf;
        }


        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching all service types.");  
            
            var serviceTypes = await _mediator.Send(new GetAllServiceTypeQuery(),cancellationToken);

            return View(serviceTypes);
        }

        [HttpGet("Create-Service-Type")]
        public IActionResult CreateServiceType()
        {
            return View();
        }

        [HttpPost("Create-Service-Type")]
        public async Task<IActionResult> CreateServiceType(CreateServiceTypeCommand command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating a new service type with name: {Name}", command.Name);
            if (!ModelState.IsValid)
            {   
                _logger.LogWarning("Model state is invalid for creating service type: {ModelStateErrors}", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _notyf.Error("Please correct the errors in the form.");
                return View(command);
            }

            

            var result = await _mediator.Send(command, cancellationToken);

           

            if (result.Success)
            {
                _logger.LogInformation("Service type created successfully with name: {Name}", command.Name);
                _notyf.Success("Service type created successfully!");
                return RedirectToAction(nameof(Index));
            }

            _logger.LogError("Failed to create service type with name: {Name}", command.Name);
            _notyf.Error(result.ErrorMessage);
            return View("Error", null);
        }
    }
}
