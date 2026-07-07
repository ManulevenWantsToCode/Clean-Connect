using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
