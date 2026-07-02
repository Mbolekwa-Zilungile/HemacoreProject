using HemaCore.Data;
using HemaCore.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
            var categories = await _context.TestCategories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var testTypeCountByCategory = await _context.TestTypes
                .GroupBy(t => t.CategoryId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.TestTypeCountByCategory = testTypeCountByCategory;
            ViewBag.CategoryCount = categories.Count;

            return View(categories);
        }

        #endregion

        #region TestTypes

        public async Task<IActionResult> TestTypes()
        {
            var testtype =  await _context.TestTypes
                .Include(t => t.Category)
                .OrderBy(t => t.TestTypeName)
                .ToListAsync();

            return View(testtype);
        }


        #endregion

        #region Sample Type

        public async Task<IActionResult> Sampletypes()
        {
            var sampletype = await _context.SampleTypes.OrderBy(t => t.SampleName).ToListAsync();
            return View(sampletype);
        }
        #endregion
        #region Doctor

        #endregion
    }
}
