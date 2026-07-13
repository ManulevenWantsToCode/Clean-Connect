using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    public class WorkerController : Controller
    {
        [HttpGet("Create-Worker-Profile")]
        public IActionResult CreateWorkerProfile()
        {
            return View();
        }
    }
}
