using Microsoft.AspNetCore.Mvc;

namespace HemaCore.Controllers
{
    public class DoctorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
