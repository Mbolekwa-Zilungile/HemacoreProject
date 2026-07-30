using Microsoft.AspNetCore.Mvc;

namespace HemaCore.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
