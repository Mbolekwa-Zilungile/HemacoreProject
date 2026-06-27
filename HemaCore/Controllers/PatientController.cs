using Microsoft.AspNetCore.Mvc;

namespace HemaCore.Controllers
{
    public class PatientController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
