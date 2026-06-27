using HemaCore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Controllers
{
    public class ManagerController : Controller
    {
        private AppDbContext _context;

        public ManagerController(AppDbContext context) 
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        #region Test Catagories
        public async Task<IActionResult> TestCategories()
        {
            var category = await _context.TestCategories.OrderBy(c => c.CategoryName).ToListAsync();

            //var testTypes = await _context.TestTypes.ToListAsync();

            //var testTypeCount = testTypes
            //    .GroupBy(t => t.CategoryID)
            //    .ToDictionary(g => g.Key, g => g.Count());

            //ViewBag.TestTypeCount = testTypeCount;

            //ViewBag.CategoryCount = category.Count;

            return View(category);
        }

        #endregion

        #region Doctor
        public async Task<IActionResult> Doctors()
        {
            var doctors = await _context.Doctors.Include(x=> x.User).OrderBy(c=> c.Surname).ThenBy(c => c.Name).ToListAsync();
            @ViewBag.DoctorCount = doctors.Count();
            return View(doctors);
        }
        #endregion
    }
}
