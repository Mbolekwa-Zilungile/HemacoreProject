using Microsoft.AspNetCore.Mvc;

namespace HemaCore.Controllers
{
    public class LabTechnicianController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult ReceiveSample()
        {
            return View();
        }
    }
}
