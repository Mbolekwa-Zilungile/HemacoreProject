using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.Enums;
using HemaCore.Services;
using HemaCore.ViewModels.ManagerVM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Reflection.Metadata;
using Twilio.Rest.Trunking.V1;

namespace HemaCore.Controllers
{
    public class ManagerController : Controller
    {

        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;
        private readonly IEmailService _emailService;

        public ManagerController(
            AppDbContext context,
            UserManager<Users> userManager,
            SignInManager<Users> signInManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }


        #region Index

        public async Task<IActionResult> Index()
        {
            // 1. ACTIVE OUT-OF-STOCK CONSUMABLES
            var outOfStockConsumables = await _context.Consumables
                .Include(c => c.Supplier)
                .Where(c =>
                    c.IsActive &&
                    c.QuantityInHand == 0)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();

            // 2. ACTIVE LOW-STOCK CONSUMABLES
            var lowStockConsumables = await _context.Consumables
                .Include(c => c.Supplier)
                .Where(c =>
                    c.IsActive &&
                    c.QuantityInHand > 0 &&
                    c.QuantityInHand <= c.ReorderLevel)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();

            // 3. ACTIVE CONSUMABLES WITHOUT AN ACTIVE SUPPLIER
            var consumablesWithoutSupplier = await _context.Consumables
                .Where(c =>
                    c.IsActive &&
                    (
                        c.SupplierId == null ||
                        !_context.Suppliers.Any(s =>
                            s.SupplierId == c.SupplierId &&
                            s.IsActive)
                    ))
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();

            // 4. ACTIVE TEST TYPES WITHOUT ASSIGNED TECHNICIANS
            var testTypesWithoutTechnician = await _context.TestTypes
                .Where(t =>
                    t.IsActive &&
                    !t.TechnicianTestType.Any())
                .OrderBy(t => t.TestTypeName)
                .ToListAsync();

            // 5. RECENT ORDERS
            // Only include orders associated with an active supplier.
            var recentOrders = await _context.Orders
             .Include(o => o.Supplier)
             .Where(o =>
                 o.Supplier != null &&
                 o.Supplier.IsActive &&
                 o.Status == HemaCore.Models.Enums.OrderStatus.Ordered)
             .OrderByDescending(o => o.OrderDate)
             .Take(5)
             .ToListAsync();

            // 6. PENDING ORDERS COUNT
            var pendingOrdersCount = await _context.Orders
                .CountAsync(o =>
                    o.Status == HemaCore.Models.Enums.OrderStatus.Ordered &&
                    o.Supplier != null &&
                    o.Supplier.IsActive);

            // 7. ACTIVE TEST CATEGORIES COUNT
            var testCategoryCount = await _context.TestCategories
                .CountAsync(c => c.IsActive);

            // 8. ACTIVE TEST TYPES COUNT
            var testTypeCount = await _context.TestTypes
                .CountAsync(t => t.IsActive);

            // 9. ACTIVE DOCTORS COUNT
            var doctorCount = await _context.Doctors
                .CountAsync(d => d.IsActive);

            // 10. ACTIVE LAB TECHNICIANS COUNT
            var technicianCount = await _context.LabTechnicians
                .CountAsync(t => t.IsActive);

            // 11. BUILD DASHBOARD VIEW MODEL
            var vm = new ManagerDashboardVM
            {
                LowStockConsumables = lowStockConsumables,
                OutOfStockConsumables = outOfStockConsumables,
                ConsumablesWithoutSupplier = consumablesWithoutSupplier,
                TestTypesWithoutTechnician = testTypesWithoutTechnician,
                RecentOrders = recentOrders,

                PendingOrdersCount = pendingOrdersCount,

                TestCategoryCount = testCategoryCount,
                TestTypeCount = testTypeCount,
                DoctorCount = doctorCount,
                TechnicianCount = technicianCount,

                LowStockCount = lowStockConsumables.Count,
                OutOfStockCount = outOfStockConsumables.Count,

                TotalStockAlerts =
                    lowStockConsumables.Count +
                    outOfStockConsumables.Count
            };

            // 12. RETURN DASHBOARD VIEW
            return View(vm);
        }

        #endregion

        #region Test Categories
        public async Task<IActionResult> TestCategories()
        {
            var categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var testTypeCountByCategory = await _context.TestTypes
                .GroupBy(t => t.CategoryId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.TestTypeCountByCategory = testTypeCountByCategory;
            ViewBag.CategoryCount = categories.Count;

            return View(categories);
        }

        public async Task<IActionResult> InactiveTestCategories()
        {
            var categories = await _context.TestCategories
                .Where(c => !c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var testTypeCountByCategory = await _context.TestTypes
                .GroupBy(t => t.CategoryId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.TestTypeCountByCategory = testTypeCountByCategory;
            ViewBag.CategoryCount = categories.Count;

            return View(categories);
        }

        public IActionResult AddCategory()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(TestCategory category)
        {
            if (!ModelState.IsValid)
            {
                return View(category);
            }

            var existingCategory = await _context.TestCategories.FirstOrDefaultAsync(x => x.CategoryName == category.CategoryName);

            if (existingCategory != null)
            {
                ModelState.AddModelError("CategoryName", "This category already exist.");
                return View(category);
            }

            _context.TestCategories.Add(category);
            category.IsActive = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{category.CategoryName} added successfully.";
            return RedirectToAction("TestCategories");
        }

        public IActionResult EditCategory(int id)
        {
            var category = _context.TestCategories
                .FirstOrDefault(c => c.CategoryId == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(TestCategory updatedCategory)
        {
            if (!ModelState.IsValid)
            {
                return View(updatedCategory);
            }

            // Find the category being updated
            var category = await _context.TestCategories
                .FirstOrDefaultAsync(c =>
                    c.CategoryId == updatedCategory.CategoryId);

            if (category == null)
            {
                return NotFound();
            }

            // Check for another category with the same name
            // Only happens when the Update button is submitted.
            bool duplicateExists = await _context.TestCategories
                .AnyAsync(c =>
                    c.CategoryId != updatedCategory.CategoryId &&
                    c.IsActive &&
                    c.CategoryName.Trim().ToLower() ==
                    updatedCategory.CategoryName.Trim().ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "CategoryName",
                    "A category with this name already exists."
                );

                return View(updatedCategory);
            }

            // Update the existing category
            category.CategoryName =
                updatedCategory.CategoryName.Trim();

            category.CategoryDescription =
                updatedCategory.CategoryDescription;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{category.CategoryName} updated successfully.";

            return RedirectToAction("TestCategories");


        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id)
        {
            var category = _context.TestCategories
                .FirstOrDefault(c => c.CategoryId == id);

            if (category == null)
            {
                return NotFound();
            }

            bool hasTestTypes = _context.TestTypes
                .Any(t => t.CategoryId == id);

            if (hasTestTypes)
            {
                TempData["Error"] =
                    $"Cannot delete {category.CategoryName} because it has test types assigned to it.";

                return RedirectToAction("TestCategories");
            }

            category.IsActive = false;
            _context.SaveChanges();

            TempData["Success"] = $"{category.CategoryName} is deactivated successfully.";

            return RedirectToAction("TestCategories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateCategory(int id)
        {
            var category = await _context.TestCategories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                TempData["Error"] = "Test category not found.";
                return RedirectToAction("InactiveCategories");
            }

            category.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{category.CategoryName} is reactivated successfully.";

            return RedirectToAction("InactiveTestCategories");
        }

        public async Task<IActionResult> CategoryDetails(int id)
        {
            var category = await _context.TestCategories
                .Include(c => c.TestTypes.Where(t => t.IsActive))
                    .ThenInclude(t => t.SampleType)
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                TempData["Error"] = "Test category not found.";
                return RedirectToAction("TestCategories");
            }

            return View(category);
        }


        [HttpGet]
        public async Task<IActionResult> SearchCategoriesLive(string term = "")
        {
            var query = _context.TestCategories
                .Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(c =>
                    c.CategoryName.Contains(term) ||
                    (c.CategoryDescription != null &&
                     c.CategoryDescription.Contains(term)));
            }

            var categories = await query
                .OrderBy(c => c.CategoryName)
                .Select(c => new
                {
                    categoryId = c.CategoryId,
                    categoryName = c.CategoryName,
                    categoryDescription = c.CategoryDescription,
                    isActive = c.IsActive,
                    testTypeCount = _context.TestTypes.Count(t =>
                        t.CategoryId == c.CategoryId)
                })
                .ToListAsync();

            return Json(categories);
        }

        #endregion

        #region TestTypes

        public async Task<IActionResult> TestTypes()
        {
            var testtype = await _context.TestTypes
                .Include(t => t.Category)
                .Where(c => c.IsActive)
                .OrderBy(t => t.TestTypeName)
                .ToListAsync();

            return View(testtype);
        }
        public async Task<IActionResult> InactiveTestTypes()
        {
            var testtype = await _context.TestTypes
                .Include(t => t.Category)
                .Where(c => !c.IsActive)
                .OrderBy(t => t.TestTypeName)
                .ToListAsync();

            return View(testtype);
        }

        [HttpGet]
        public async Task<IActionResult> AddTestType()
        {
            // Load active categories
            ViewBag.Categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();


            // Load active sample types
            ViewBag.SampleTypes = await _context.SampleTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SampleName)
                .ToListAsync();


            // Load active consumables
            var consumables = await _context.Consumables
                .Where(c => c.IsActive)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();


            // Load active technicians
            var technicians = await _context.LabTechnicians
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Surname)
                .ToListAsync();


            var model = new TestTypeViewModel
            {
                Consumables = consumables.Select(c => new ConsumableVM
                {
                    ConsumableID = c.ConsumableId,
                    ConsumableName = c.ConsumableName,
                    IsSelected = false,
                    QuantityUsed = 0
                }).ToList(),

                Technicians = technicians.Select(t => new TechnicianVM
                {
                    TechnicianID = t.LabTechnicianId,
                    TechnicianName = $"{t.Name} {t.Surname}",
                    IsSelected = false
                }).ToList()
            };


            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTestType(TestTypeViewModel model)
        {

            ViewBag.Categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            ViewBag.SampleTypes = await _context.SampleTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SampleName)
                .ToListAsync();


            // =====================================================
            // VALIDATE MODEL
            // =====================================================

            if (!ModelState.IsValid)
            {
                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // CHECK CATEGORY
            // =====================================================

            var category = await _context.TestCategories
                .FirstOrDefaultAsync(c =>
                    c.CategoryId == model.CategoryID &&
                    c.IsActive);

            if (category == null)
            {
                ModelState.AddModelError(
                    "CategoryID",
                    "Please select a valid category."
                );

                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // CHECK SAMPLE TYPE
            // =====================================================

            var sampleType = await _context.SampleTypes
                .FirstOrDefaultAsync(s =>
                    s.SampleTypeId == model.SampleTypeID &&
                    s.IsActive);

            if (sampleType == null)
            {
                ModelState.AddModelError(
                    "SampleTypeID",
                    "Please select a valid sample type."
                );

                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // CHECK DUPLICATE TEST TYPE
            // =====================================================

            var existingTestType = await _context.TestTypes
                .FirstOrDefaultAsync(t =>
                    t.TestTypeName == model.TestTypeName);

            if (existingTestType != null)
            {
                ModelState.AddModelError(
                    "TestTypeName",
                    "This test type already exists."
                );

                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // CHECK NORMAL RANGE
            // =====================================================

            if (model.NormalRangeMin > model.NormalRangeMax)
            {
                ModelState.AddModelError(
                    "NormalRangeMin",
                    "Minimum normal range cannot be greater than maximum normal range."
                );

                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // CREATE TEST TYPE
            // =====================================================

            var testType = new TestType
            {
                TestTypeName = model.TestTypeName,
                UnitOfMeasurement = model.UnitOfMeasurement,
                NormalRangeMin = model.NormalRangeMin,
                NormalRangeMax = model.NormalRangeMax,
                TurnAroundTime = model.TurnAroundTime,
                CategoryId = model.CategoryID,
                SampleTypeId = model.SampleTypeID,
                IsActive = true
            };

            _context.TestTypes.Add(testType);

            await _context.SaveChangesAsync();



            var selectedConsumables = model.Consumables?
                .Where(c => c.IsSelected)
                .ToList()
                ?? new List<ConsumableVM>();


            if (selectedConsumables.Any())
            {
                // Get valid consumable IDs from database
                var selectedConsumableIds = selectedConsumables
                    .Select(c => c.ConsumableID)
                    .ToList();

                var validConsumableIds = await _context.Consumables
                    .Where(c =>
                        selectedConsumableIds.Contains(c.ConsumableId)
                    // If your Consumable model has IsActive,
                    // you can also add:
                    // && c.IsActive
                    )
                    .Select(c => c.ConsumableId)
                    .ToListAsync();


                foreach (var consumable in selectedConsumables)
                {
                    // Only save consumables that actually exist
                    if (!validConsumableIds.Contains(consumable.ConsumableID))
                    {
                        continue;
                    }


                    // If selected but quantity is 0/null,
                    // automatically use 1
                    var quantity = consumable.QuantityUsed <= 0
                        ? 1
                        : consumable.QuantityUsed;


                    var testTypeConsumable = new TestTypeConsumable
                    {
                        TestTypeId = testType.TestTypeId,

                        ConsumableId = consumable.ConsumableID,

                        QuantityNeeded = quantity
                    };


                    _context.TestTypeConsumables.Add(
                        testTypeConsumable
                    );
                }


                await _context.SaveChangesAsync();
            }


            // =====================================================
            // ASSIGN TECHNICIANS
            // =====================================================

            var selectedTechnicianIds = model.Technicians?
                .Where(t => t.IsSelected)
                .Select(t => t.TechnicianID)
                .ToList()
                ?? new List<int>();


            if (selectedTechnicianIds.Any())
            {
                var activeTechnicianIds = await _context.LabTechnicians
                    .Where(t =>
                        t.IsActive &&
                        selectedTechnicianIds.Contains(t.LabTechnicianId))
                    .Select(t => t.LabTechnicianId)
                    .ToListAsync();


                foreach (var technicianId in activeTechnicianIds)
                {
                    var technicianTestType = new TechnicianTestType
                    {
                        LabTechnicianId = technicianId,
                        TestTypeId = testType.TestTypeId
                    };


                    _context.TechnicianTestTypes.Add(
                        technicianTestType
                    );
                }


                await _context.SaveChangesAsync();
            }



            TempData["Success"] =
                $"{model.TestTypeName} added successfully.";


            return RedirectToAction("TestTypes");
        }


        private async Task ReloadConsumables(TestTypeViewModel model)
        {
            var consumables = await _context.Consumables
                .Where(c => c.IsActive)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();

            model.Consumables = consumables.Select(c =>
            {
                var existing = model.Consumables?
                    .FirstOrDefault(x => x.ConsumableID == c.ConsumableId);

                return new ConsumableVM
                {
                    ConsumableID = c.ConsumableId,
                    ConsumableName = c.ConsumableName,
                    IsSelected = existing?.IsSelected ?? false,
                    QuantityUsed = existing?.QuantityUsed ?? 0
                };
            }).ToList();
        }

        private async Task ReloadTechnicians(TestTypeViewModel model)
        {
            var technicians = await _context.LabTechnicians
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Surname)
                .ToListAsync();


            model.Technicians = technicians.Select(t =>
            {
                var existing = model.Technicians?
                    .FirstOrDefault(x =>
                        x.TechnicianID == t.LabTechnicianId);


                return new TechnicianVM
                {
                    TechnicianID = t.LabTechnicianId,

                    TechnicianName =
                        $"{t.Name} {t.Surname}",

                    IsSelected =
                        existing?.IsSelected ?? false
                };

            }).ToList();
        }

        public async Task<IActionResult> TestTypeDetails(int id)
        {
            var testType = await _context.TestTypes
                .Include(t => t.Category)
                .Include(t => t.SampleType)

                // Assigned technicians
                .Include(t => t.TechnicianTestType)
                    .ThenInclude(tt => tt.LabTechnician)

                // Assigned consumables
                .Include(t => t.TestTypeConsumables)
                    .ThenInclude(tc => tc.Consumable)

                .FirstOrDefaultAsync(t => t.TestTypeId == id);

            if (testType == null)
            {
                return NotFound();
            }

            return View(testType);
        }

        [HttpGet]
        public async Task<IActionResult> EditTestType(int id)
        {
            var testType = await _context.TestTypes
                .Include(t => t.TestTypeConsumables)
                    .ThenInclude(tc => tc.Consumable)

                .Include(t => t.TechnicianTestType)
                    .ThenInclude(tt => tt.LabTechnician)

                .FirstOrDefaultAsync(t => t.TestTypeId == id);


            if (testType == null)
            {
                return NotFound();
            }


            // =====================================================
            // CATEGORIES
            // =====================================================

            ViewBag.Categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();


            // =====================================================
            // SAMPLE TYPES
            // =====================================================

            ViewBag.SampleTypes = await _context.SampleTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SampleName)
                .ToListAsync();


            // =====================================================
            // ACTIVE CONSUMABLES
            // =====================================================

            var consumables = await _context.Consumables
                .Where(c => c.IsActive)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();


            // =====================================================
            // ACTIVE TECHNICIANS
            // =====================================================

            var technicians = await _context.LabTechnicians
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Surname)
                .ToListAsync();


            // =====================================================
            // CREATE VIEW MODEL
            // =====================================================

            var model = new TestTypeViewModel
            {
                TestTypeId = testType.TestTypeId,

                TestTypeName = testType.TestTypeName,

                UnitOfMeasurement = testType.UnitOfMeasurement,

                NormalRangeMin = testType.NormalRangeMin,

                NormalRangeMax = testType.NormalRangeMax,

                TurnAroundTime = testType.TurnAroundTime,

                IsActive = testType.IsActive,

                CategoryID = testType.CategoryId,

                SampleTypeID = testType.SampleTypeId
            };


            // =====================================================
            // LOAD CONSUMABLES
            // =====================================================

            foreach (var consumable in consumables)
            {
                var existingConsumable =
                    testType.TestTypeConsumables
                        .FirstOrDefault(tc =>
                            tc.ConsumableId == consumable.ConsumableId);


                model.Consumables.Add(
                    new ConsumableVM
                    {
                        ConsumableID = consumable.ConsumableId,

                        ConsumableName = consumable.ConsumableName,

                        IsSelected = existingConsumable != null,

                        QuantityUsed = existingConsumable?.QuantityNeeded ?? 0
                    }
                );
            }


            // =====================================================
            // LOAD TECHNICIANS
            // =====================================================

            foreach (var technician in technicians)
            {
                var existingTechnician =
                    testType.TechnicianTestType
                        .Any(tt =>
                            tt.LabTechnicianId ==
                            technician.LabTechnicianId);


                model.Technicians.Add(
                    new TechnicianVM
                    {
                        TechnicianID = technician.LabTechnicianId,

                        TechnicianName =
                            $"{technician.Name} {technician.Surname}",

                        IsSelected = existingTechnician
                    }
                );
            }


            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTestType(TestTypeViewModel model)
        {
            // =====================================================
            // LOAD DROPDOWNS
            // =====================================================

            ViewBag.Categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();


            ViewBag.SampleTypes = await _context.SampleTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SampleName)
                .ToListAsync();


            // =====================================================
            // VALIDATION
            // =====================================================

            if (!ModelState.IsValid)
            {
                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // CHECK NORMAL RANGE
            // =====================================================

            if (model.NormalRangeMin > model.NormalRangeMax)
            {
                ModelState.AddModelError(
                    "NormalRangeMin",
                    "Minimum normal range cannot be greater than maximum normal range."
                );

                await ReloadConsumables(model);
                await ReloadTechnicians(model);

                return View(model);
            }


            // =====================================================
            // FIND TEST TYPE
            // =====================================================

            var testType = await _context.TestTypes
                .Include(t => t.TestTypeConsumables)
                .Include(t => t.TechnicianTestType)
                .FirstOrDefaultAsync(t =>
                    t.TestTypeId == model.TestTypeId);


            if (testType == null)
            {
                return NotFound();
            }


            // =====================================================
            // UPDATE TEST TYPE DETAILS
            // =====================================================

            testType.TestTypeName =
                model.TestTypeName;

            testType.UnitOfMeasurement =
                model.UnitOfMeasurement;

            testType.NormalRangeMin =
                model.NormalRangeMin;

            testType.NormalRangeMax =
                model.NormalRangeMax;

            testType.TurnAroundTime =
                model.TurnAroundTime;

            testType.CategoryId =
                model.CategoryID;

            testType.SampleTypeId =
                model.SampleTypeID;

            testType.IsActive =
                model.IsActive;


            // =====================================================
            // REMOVE OLD CONSUMABLES
            // =====================================================

            if (testType.TestTypeConsumables != null &&
                testType.TestTypeConsumables.Any())
            {
                _context.TestTypeConsumables.RemoveRange(
                    testType.TestTypeConsumables
                );
            }


            // =====================================================
            // ADD SELECTED CONSUMABLES
            // =====================================================

            var selectedConsumables =
                model.Consumables?
                    .Where(c => c.IsSelected)
                    .ToList()
                ?? new List<ConsumableVM>();


            foreach (var consumable in selectedConsumables)
            {
                // Automatically use quantity 1
                // if the selected consumable has no quantity.
                var quantity =
                    consumable.QuantityUsed <= 0
                        ? 1
                        : consumable.QuantityUsed;


                // Make sure the consumable exists
                var exists =
                    await _context.Consumables
                        .AnyAsync(c =>
                            c.ConsumableId ==
                            consumable.ConsumableID &&
                            c.IsActive);


                if (!exists)
                {
                    continue;
                }


                _context.TestTypeConsumables.Add(
                    new TestTypeConsumable
                    {
                        TestTypeId =
                            testType.TestTypeId,

                        ConsumableId =
                            consumable.ConsumableID,

                        QuantityNeeded =
                            quantity
                    }
                );
            }


            if (testType.TechnicianTestType != null &&
                testType.TechnicianTestType.Any())
            {
                _context.TechnicianTestTypes.RemoveRange(
                    testType.TechnicianTestType
                );
            }


            var selectedTechnicians =
                model.Technicians?
                    .Where(t => t.IsSelected)
                    .Select(t => t.TechnicianID)
                    .ToList()
                ?? new List<int>();


            if (selectedTechnicians.Any())
            {
                var activeTechnicians =
                    await _context.LabTechnicians
                        .Where(t =>
                            t.IsActive &&
                            selectedTechnicians.Contains(
                                t.LabTechnicianId))
                        .Select(t =>
                            t.LabTechnicianId)
                        .ToListAsync();


                foreach (var technicianId in activeTechnicians)
                {
                    _context.TechnicianTestTypes.Add(
                        new TechnicianTestType
                        {
                            TestTypeId =
                                testType.TestTypeId,

                            LabTechnicianId =
                                technicianId
                        }
                    );
                }
            }

            await _context.SaveChangesAsync();


            TempData["Success"] =
                $"{model.TestTypeName} updated successfully.";


            return RedirectToAction("TestTypes");
        }



        public async Task<IActionResult> DeleteTestType(int id)
        {
            var testType = await _context.TestTypes
                .Include(t => t.TechnicianTestType)
                .FirstOrDefaultAsync(t => t.TestTypeId == id);

            if (testType == null)
            {
                return NotFound();
            }

            // TODO: Replace with the actual query for ongoing test requests.
            // This must check active requests, not technician assignments.
            bool hasOngoingTests = false;

            if (hasOngoingTests)
            {
                TempData["Error"] =
                    $"Cannot deactivate {testType.TestTypeName} because it is linked to ongoing test requests.";

                return RedirectToAction("TestTypes");
            }

            // Soft delete: retain technician links and historical records.
            testType.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{testType.TestTypeName} has been deactivated successfully.";

            return RedirectToAction("TestTypes");
        }

        public async Task<IActionResult> ActivateTestType(int id)
        {
            var testType = await _context.TestTypes
                .Include(t => t.TechnicianTestType)
                .Include(t => t.TestTypeConsumables)
                .FirstOrDefaultAsync(t => t.TestTypeId == id);

            if (testType == null)
            {
                return NotFound();
            }
          

            testType.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{testType.TestTypeName} activated successfully.";

            return RedirectToAction("InactiveTestTypes");
        }


        [HttpGet]
        public async Task<IActionResult> AssignTechnicianToTestTypes(int testTypeId)
        {
            var testType = await _context.TestTypes
                .FirstOrDefaultAsync(t => t.TestTypeId == testTypeId);

            if (testType == null)
            {
                TempData["Error"] = "Test type not found.";
                return RedirectToAction("TestTypes");
            }

            var assignedTechnicianIds = await _context.TechnicianTestTypes
                .Where(x => x.TestTypeId == testTypeId)
                .Select(x => x.LabTechnicianId)
                .ToListAsync();

            var availableTechnicians = await _context.LabTechnicians
                .Where(t => t.IsActive &&
                            !assignedTechnicianIds.Contains(t.LabTechnicianId))
                .OrderBy(t => t.Name)
                .ThenBy(t => t.Surname)
                .ToListAsync();

            var viewModel = new HemaCore.ViewModels.ManagerVM
                .AssignTechnicianViewModel
            {
                TestTypeId = testType.TestTypeId,
                TestTypeName = testType.TestTypeName,
                AvailableTechnicians = availableTechnicians
            };

            return View("AssignTechnicianToTestTypes", viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTechnicianToTestTypes(
            HemaCore.ViewModels.ManagerVM.AssignTechnicianViewModel model)
        {
            var testType = await _context.TestTypes
                .FirstOrDefaultAsync(t => t.TestTypeId == model.TestTypeId);

            if (testType == null)
            {
                TempData["Error"] = "Test type not found.";
                return RedirectToAction("TestTypes");
            }

            var selectedIds = model.SelectedTechnicianIds?
                .Distinct()
                .ToList() ?? new List<int>();

            if (!selectedIds.Any())
            {
                TempData["Error"] = "Please select at least one technician.";
                return RedirectToAction(
                    "AssignTechnicianToTestTypes",
                    new { testTypeId = model.TestTypeId });
            }

            var activeTechnicianIds = await _context.LabTechnicians
                .Where(t => t.IsActive && selectedIds.Contains(t.LabTechnicianId))
                .Select(t => t.LabTechnicianId)
                .ToListAsync();

            if (activeTechnicianIds.Count != selectedIds.Count)
            {
                TempData["Error"] =
                    "One or more selected technicians are invalid or inactive.";

                return RedirectToAction(
                    "AssignTechnicianToTestTypes",
                    new { testTypeId = model.TestTypeId });
            }

            var alreadyAssignedIds = await _context.TechnicianTestTypes
                .Where(x => x.TestTypeId == model.TestTypeId &&
                            selectedIds.Contains(x.LabTechnicianId))
                .Select(x => x.LabTechnicianId)
                .ToListAsync();

            var newAssignments = selectedIds
                .Except(alreadyAssignedIds)
                .Select(technicianId => new TechnicianTestType
                {
                    TestTypeId = model.TestTypeId,
                    LabTechnicianId = technicianId
                })
                .ToList();

            if (!newAssignments.Any())
            {
                TempData["Error"] =
                    "All selected technicians are already assigned to this test type.";

                return RedirectToAction(
                    "AssignTechnicianToTestTypes",
                    new { testTypeId = model.TestTypeId });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.TechnicianTestTypes.AddRange(newAssignments);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    $"{newAssignments.Count} technician(s) assigned successfully.";

                return RedirectToAction(
                    "TestTypeDetails",
                    new { id = model.TestTypeId });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        [HttpGet]
        public async Task<IActionResult> SearchTestTypesLive(string term = "")
        {
            var query = _context.TestTypes
                .Include(t => t.Category)
                .AsQueryable();

            // Search by test name or category name
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(t =>
                    t.TestTypeName.Contains(term) ||
                    (t.Category != null &&
                     t.Category.CategoryName.Contains(term)));
            }

            // Return active test types on the active page,
            // and inactive test types on the inactive page.
            var currentAction = HttpContext.Request.Headers["Referer"]
                .ToString();

            bool showInactive = currentAction.Contains(
                "InactiveTestTypes",
                StringComparison.OrdinalIgnoreCase);

            query = query.Where(t => t.IsActive != showInactive);

            var testTypes = await query
                .OrderBy(t => t.TestTypeName)
                .Select(t => new
                {
                    testTypeId = t.TestTypeId,
                    name = t.TestTypeName,
                    category = t.Category != null
                        ? t.Category.CategoryName
                        : "Not assigned",
                    turnaround = t.TurnAroundTime,
                    isActive = t.IsActive
                })
                .ToListAsync();

            return Json(testTypes);
        }

        #endregion

        #region Sample Type

        public async Task<IActionResult> SampleTypes()
        {
            var sampletype = await _context.SampleTypes
                .Where(c => c.IsActive)
                .OrderBy(t => t.SampleName)
                .ToListAsync();
            return View(sampletype);
        }

        public async Task<IActionResult> InactiveSampleTypes()
        {
            var sampletype = await _context.SampleTypes
                .Where(c => !c.IsActive)
                .OrderBy(t => t.SampleName)
                .ToListAsync();
            return View(sampletype);
        }

        public IActionResult AddSampleType()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> AddSampleType(SampleType sampletype)
        {
            if (!ModelState.IsValid)
            {
                return View(sampletype);
            }

            var existingsampletype = await _context.SampleTypes.FirstOrDefaultAsync(x => x.SampleName == sampletype.SampleName);

            if (existingsampletype != null)
            {
                ModelState.AddModelError("SampleName", "The sample type already exists");
                return View(sampletype);
            }

            _context.SampleTypes.Add(sampletype);
            sampletype.IsActive = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{sampletype.SampleName} added successfully.";
            return RedirectToAction("SampleTypes");
        }


        public async Task<IActionResult> EditSampleType(int id)
        {
            var sampleType = await _context.SampleTypes
                .FirstOrDefaultAsync(s => s.SampleTypeId == id);

            if (sampleType == null)
            {
                return NotFound();
            }

            return View(sampleType);
        }


        // POST: Edit Sample Type
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSampleType(SampleType updateSample)
        {
            if (!ModelState.IsValid)
            {
                return View(updateSample);
            }

            var sampleType = await _context.SampleTypes
                .FirstOrDefaultAsync(s =>
                    s.SampleTypeId == updateSample.SampleTypeId);

            if (sampleType == null)
            {
                return NotFound();
            }

            // Update fields
            sampleType.SampleName = updateSample.SampleName;
            sampleType.Description = updateSample.Description;

            // Save changes to SQL Server
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{sampleType.SampleName} updated successfully.";

            return RedirectToAction("SampleTypes");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateSampleType(int id)
        {
            var sample = await _context.SampleTypes
                .FirstOrDefaultAsync(s => s.SampleTypeId == id);

            if (sample == null)
            {
                return NotFound();
            }

            // Check if this SampleType is being used by any TestType
            bool isUsed = await _context.TestTypes
                .AnyAsync(t => t.SampleTypeId == id);

            if (isUsed)
            {
                TempData["Error"] =
                    $"Cannot delete {sample.SampleName} because it is being used by a test type.";

                return RedirectToAction("SampleTypes");
            }

            // Soft delete
            sample.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{sample.SampleName} deleted successfully.";

            return RedirectToAction("SampleTypes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateSampletype(int id)
        {
            var sample = await _context.SampleTypes
                .FirstOrDefaultAsync(s => s.SampleTypeId == id);

            if (sample == null)
            {
                return NotFound();
            }

            sample.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{sample.SampleName} activated successfully.";

            return RedirectToAction("InactiveSampleTypes");
        }

        // GET: Manager/SampleTypeDetails/5
        public async Task<IActionResult> SampleTypeDetails(int id)
        {
            var sampleType = await _context.SampleTypes
                .Include(s => s.TestTypes)
                .Where(s => s.IsActive)
                .FirstOrDefaultAsync(s => s.SampleTypeId == id);

            if (sampleType == null)
            {
                TempData["Error"] = "Sample type not found.";
                return RedirectToAction("SampleTypes");
            }

            return View(sampleType);
        }

        // =====================================================
        // LIVE SEARCH SAMPLE TYPES
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> SearchSampleTypesLive(
            string term = "",
            string status = "active")
        {
            var query = _context.SampleTypes.AsQueryable();

            // Filter by active or inactive status.
            if (string.Equals(
                status,
                "inactive",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => !s.IsActive);
            }
            else
            {
                query = query.Where(s => s.IsActive);
            }

            // Search by sample type name or description.
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(s =>
                    s.SampleName.Contains(term) ||
                    (s.Description != null &&
                     s.Description.Contains(term)));
            }

            // Return the properties expected by JavaScript.
            var results = await query
                .OrderBy(s => s.SampleName)
                .Select(s => new
                {
                    sampleTypeId = s.SampleTypeId,
                    sampleName = s.SampleName,
                    description = s.Description,
                    isActive = s.IsActive
                })
                .ToListAsync();

            return Json(results);
        }
        #endregion

        #region Consumable

        public async Task<IActionResult> Consumables()
        {
            var consumables = await _context.Consumables
                .Include(c => c.Supplier)
                .Where(c => c.IsActive)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();
            return View(consumables);
        }

        public async Task<IActionResult> InactiveConsumables()
        {
            var consumables = await _context.Consumables
                .Include(c => c.Supplier)
                .Where(c => !c.IsActive)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();
            return View(consumables);
        }


        [HttpGet]
        public async Task<IActionResult> SearchConsumablesLive(
            string term = "",
            string status = "active")
        {
            var query = _context.Consumables
                .Include(c => c.Supplier)
                .AsQueryable();

            // Filter by active or inactive consumables.
            if (string.Equals(
                status,
                "inactive",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => !c.IsActive);
            }
            else
            {
                query = query.Where(c => c.IsActive);
            }

            // Search by consumable name or supplier name.
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(c =>
                    c.ConsumableName.Contains(term) ||
                    (c.Supplier != null &&
                     c.Supplier.SupplierName.Contains(term)));
            }

            var results = await query
                .OrderBy(c => c.ConsumableName)
                .Select(c => new
                {
                    consumableId = c.ConsumableId,
                    consumableName = c.ConsumableName,

                    supplierName =
                        c.Supplier != null && c.Supplier.IsActive
                            ? c.Supplier.SupplierName
                            : "",

                    reorderLevel = c.ReorderLevel,
                    quantityInHand = c.QuantityInHand,
                    status = c.Status.ToString(),
                    isActive = c.IsActive
                })
                .ToListAsync();

            return Json(results);
        }


        public async Task<IActionResult> AddConsumable()
        {
            ViewBag.Suppliers = await _context.Suppliers
                .Where(x => x.IsActive)
                .OrderBy(x => x.SupplierName).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConsumable(Consumable consumable)
        {
            ModelState.Remove("SupplierName");

            if (!ModelState.IsValid)
            {
                ViewBag.Suppliers = await _context.Suppliers
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SupplierName)
                    .ToListAsync();

                return View(consumable);
            }
            var existinGCONSUMABLE = await _context.Consumables.FirstOrDefaultAsync(x => x.ConsumableName == consumable.ConsumableName);

            if (existinGCONSUMABLE != null)
            {
                ModelState.AddModelError("ConsumableName", "The consumable already exist.");
                return View(consumable);
            }
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(x => x.SupplierId == consumable.SupplierId && x.IsActive);

            if (supplier == null)
            {
                ModelState.AddModelError("SupplierId", "Please select a valid supplier.");
                ViewBag.Suppliers = await _context.Suppliers
                                   .OrderBy(x => x.SupplierName)
                                   .ToListAsync();
                return View(consumable);
            }
            consumable.IsActive = true;

            _context.Consumables.Add(consumable);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{consumable.ConsumableName} added successfully.";

            return RedirectToAction(nameof(Consumables));
        }

        [HttpGet]
        public IActionResult EditConsumable(int id)
        {
            var consumable = _context.Consumables
                .FirstOrDefault(c => c.ConsumableId == id);

            if (consumable == null)
            {
                return NotFound();
            }

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.SupplierName)
                .ToList();

            return View(consumable);
        }

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditConsumable(Consumable model)
        {
            ModelState.Remove("SupplierName");
            ModelState.Remove("OrderItems");

            bool nameExists = _context.Consumables
        .Any(c =>
            c.ConsumableName == model.ConsumableName &&
            c.ConsumableId != model.ConsumableId
        );

            if (nameExists)
            {
                ModelState.AddModelError(
                    "ConsumableName",
                    "A consumable with this name already exists."
                );
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Suppliers = _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SupplierName)
                    .ToList();

                return View(model);
            }

            var consumable = _context.Consumables
                .FirstOrDefault(c => c.ConsumableId == model.ConsumableId);

            if (consumable == null)
            {
                return NotFound();
            }

            // Update database entity
            consumable.ConsumableName = model.ConsumableName;
            consumable.ReorderLevel = model.ReorderLevel;
            consumable.QuantityInHand = model.QuantityInHand;
            consumable.SupplierId = model.SupplierId;

            _context.SaveChanges();

            TempData["Success"] = $"{consumable.ConsumableName} updated successfully.";

            return RedirectToAction(nameof(Consumables));
        }

        // ADJUST STOCK (GET)
        public IActionResult AdjustStock(int id)
        {
            var consumable = _context.Consumables
                .FirstOrDefault(c => c.ConsumableId == id);

            if (consumable == null)
                return NotFound();

            var vm = new StockAdjustmentVM
            {
                ConsumableID = consumable.ConsumableId,
                ConsumableName = consumable.ConsumableName,
                QuantityOnHand = consumable.QuantityInHand
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdjustStock(StockAdjustmentVM vm)
        {
            var consumable = _context.Consumables
                .FirstOrDefault(c => c.ConsumableId == vm.ConsumableID);

            if (consumable == null)
                return NotFound();

            switch (vm.AdjustmentType)
            {
                case "Set":
                    consumable.QuantityInHand = vm.Amount;
                    break;

                case "Increase":
                    consumable.QuantityInHand += vm.Amount;
                    break;

                case "Decrease":
                    consumable.QuantityInHand -= vm.Amount;

                    if (consumable.QuantityInHand < 0)
                        consumable.QuantityInHand = 0;

                    break;
            }

            _context.SaveChanges();

            TempData["Success"] = $"Stock for {consumable.ConsumableName} updated successfully.";

            return RedirectToAction("Consumables");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConsumable(int id)
        {
            // Find the consumable in the database
            var consumable = _context.Consumables
            .FirstOrDefault(c => c.ConsumableId == id);

            if (consumable == null)
            {
                return NotFound();
            }

            // Check if the consumable is linked to any test type
            bool isLinkedToTest = _context.TestTypeConsumables
                .Any(tc => tc.ConsumableId == id);

            if (isLinkedToTest)
            {
                TempData["Error"] =
                    $"Cannot delete this {consumable.ConsumableName} because it is linked to one or more test types.";

                return RedirectToAction("Consumables");
            }

            // Soft delete
            consumable.IsActive = false;

            // Save the change to the database
            _context.SaveChanges();

            TempData["Success"] = $"{consumable.ConsumableName} deleted successfully.";

            return RedirectToAction("Consumables");

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateConsumable(int id)
        {
            // Find the consumable in the database
            var consumable = _context.Consumables
            .FirstOrDefault(c => c.ConsumableId == id);

            if (consumable == null)
            {
                return NotFound();
            }


            consumable.IsActive = true;

            // Save the change to the database
            _context.SaveChanges();

            TempData["Success"] = $"{consumable.ConsumableName} deleted successfully.";

            return RedirectToAction("InactiveConsumables");

        }
        #endregion

        #region Supplier

        public async Task<IActionResult> Suppliers()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.SupplierName)
                .ToListAsync();

            ViewBag.SupplierCount = suppliers.Count;

            ViewBag.SupplierConsumableCount = await _context.Consumables
                .GroupBy(c => c.SupplierId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Count()
                );

            return View(suppliers);
        }
        [HttpGet]
        public async Task<IActionResult> SearchSupplierLive(string term = "")
        {
            var query = _context.Suppliers
                .Where(s => s.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(s =>
                    s.SupplierName.Contains(term) ||
                    (s.ContactPerson != null &&
                     s.ContactPerson.Contains(term)) ||
                    (s.Email != null &&
                     s.Email.Contains(term)));
            }

            var suppliers = await query
                .OrderBy(s => s.SupplierName)
                .Select(s => new
                {
                    supplierId = s.SupplierId,
                    name = s.SupplierName,
                    contactPerson = s.ContactPerson,
                    email = s.Email,
                    totalConsumables = _context.Consumables
                        .Count(c => c.SupplierId == s.SupplierId),
                    isActive = s.IsActive
                })
                .ToListAsync();

            return Json(suppliers);
        }
        public async Task<IActionResult> InactiveSuppliers()
        {
            var suppliers = await _context.Suppliers
                .Where(s => !s.IsActive)
                .OrderBy(s => s.SupplierName)
                .ToListAsync();

            ViewBag.SupplierCount = suppliers.Count;

            ViewBag.SupplierConsumableCount = await _context.Consumables
                .GroupBy(c => c.SupplierId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Count()
                );

            return View(suppliers);
        }
        public IActionResult AddSupplier()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> AddSupplier(Supplier supplier)
        {
            if (!ModelState.IsValid)
            {
                return View(supplier);
            }

            var existingSupplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierName == supplier.SupplierName);

            if (existingSupplier != null)
            {
                ModelState.AddModelError("SupplierName", "Supplier already exist.");
                return View(supplier);
            }

            var existingSupplierEmail = await _context.Suppliers.FirstOrDefaultAsync(x => x.Email == supplier.Email);

            if (existingSupplierEmail != null)
            {
                ModelState.AddModelError("Email", "Email already exist.");
                return View(supplier);
            }
            supplier.IsActive = true;
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{supplier.SupplierName} added successfully.";
            return RedirectToAction("Suppliers");

        }



        [HttpGet]
        public async Task<IActionResult> EditSupplier(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupplier(Supplier model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find the supplier being edited
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == model.SupplierId);

            if (supplier == null)
            {
                return NotFound();
            }

            // Clean input
            var supplierName = model.SupplierName.Trim();
            var email = model.Email.Trim();

            // =====================================================
            // CHECK SUPPLIER NAME
            // ONLY ACTIVE SUPPLIERS
            // EXCLUDE CURRENT SUPPLIER
            // =====================================================

            bool nameExists = await _context.Suppliers
                .AnyAsync(s =>
                    s.IsActive &&
                    s.SupplierId != supplier.SupplierId &&
                    s.SupplierName == supplierName);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.SupplierName),
                    "Another active supplier with this name already exists.");

                return View(model);
            }

            bool emailExists = await _context.Suppliers
                .AnyAsync(s =>
                    s.IsActive &&
                    s.SupplierId != supplier.SupplierId &&
                    s.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Another active supplier with this email already exists.");

                return View(model);
            }

            // =====================================================
            // UPDATE SUPPLIER
            // =====================================================

            supplier.SupplierName = supplierName;
            supplier.ContactPerson = model.ContactPerson?.Trim();
            supplier.Email = email;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{supplier.SupplierName} updated successfully.";

            return RedirectToAction(nameof(Suppliers));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }


            supplier.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{supplier.SupplierName} is deactivated successfully. Please assign a new supplier to the affected consumables to ensure uninterrupted stock replenishment";

            return RedirectToAction("Suppliers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateSupplier(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }


            supplier.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{supplier.SupplierName} has been activated successfully.";

            return RedirectToAction("InactiveSuppliers");
        }

        public async Task<IActionResult> SupplierDetails(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Consumables)
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }


        [HttpGet]
        public async Task<IActionResult> AddSupplierConsumables(int supplierId)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

            if (supplier == null)
                return NotFound();

            if (!supplier.IsActive)
                return BadRequest("Cannot assign consumables to an inactive supplier.");

            await LoadAvailableConsumables(supplierId);

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSupplierConsumables(
            int supplierId, int consumableId)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

            if (supplier == null)
                return NotFound();

            if (!supplier.IsActive)
                return BadRequest("Cannot assign consumables to an inactive supplier.");

            var consumable = await _context.Consumables
                .FirstOrDefaultAsync(c => c.ConsumableId == consumableId);

            if (consumable == null)
            {
                ModelState.AddModelError("", "Please select a valid consumable.");
                await LoadAvailableConsumables(supplierId);
                return View(supplier);
            }

            if (consumable.SupplierId != null)
            {
                ModelState.AddModelError(
                    "",
                    "This consumable is already assigned to a supplier.");

                await LoadAvailableConsumables(supplierId);
                return View(supplier);
            }

            consumable.SupplierId = supplierId;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Consumable assigned to " +
                                  supplier.SupplierName + " successfully.";

            return RedirectToAction("SupplierDetails", new { id = supplierId });
        }

        private async Task LoadAvailableConsumables(int supplierId)
        {
            var availableConsumables = await _context.Consumables
                .Where(c => c.SupplierId == null)
                .OrderBy(c => c.ConsumableName)
                .ToListAsync();

            ViewBag.AvailableConsumables = new SelectList(
                availableConsumables,
                "ConsumableId",
                "ConsumableName"
            );
        }


        #endregion

        #region Orders

        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Consumable)
                .OrderByDescending(c => c.OrderId)
                .ToListAsync();

            ViewBag.OrdersCount = orders.Count;

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> SearchOrders(string term = "")
        {
            var query = _context.Orders.AsQueryable();

            // Search only by order number.
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                query = query.Where(o => o.OrderNumber.Contains(term));
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    orderId = o.OrderId,
                    orderNumber = o.OrderNumber,
                    supplierName = o.Supplier != null
                        ? o.Supplier.SupplierName
                        : "Unknown",
                    orderDate = o.OrderDate,
                    itemCount = o.OrderItems.Count(),
                    status = o.Status.ToString()
                })
                .ToListAsync();

            var results = orders.Select(o => new
            {
                o.orderId,
                o.orderNumber,
                o.supplierName,
                orderDate = o.orderDate.ToString("yyyy-MM-dd"),
                o.itemCount,
                o.status
            });

            return Json(results);
        }
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Consumable)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveItem(int orderId, int orderItemId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound();

            var item = order.OrderItems
                .FirstOrDefault(i => i.OrderItemId == orderItemId);

            if (item == null)
                return NotFound();

            // Prevent double receiving
            if (item.Status != OrderItemStatus.Pending)
            {
                return RedirectToAction(
                    "OrderDetails",
                    new { id = orderId }
                );
            }

            item.Status = OrderItemStatus.Received;
            item.DateReceived = DateTime.Now;

            var consumable = await _context.Consumables
                .FirstOrDefaultAsync(c => c.ConsumableId == item.ConsumableId);

            if (consumable != null)
            {
                consumable.QuantityInHand += item.Quantity;
            }

            var items = order.OrderItems.ToList();

            if (items.All(i => i.Status == OrderItemStatus.Received))
            {
                order.Status = OrderStatus.Completed;
                order.DateCompleted = DateTime.Now;
            }
            else if (items.All(i => i.Status == OrderItemStatus.Cancelled))
            {
                order.Status = OrderStatus.Cancelled;
                order.DateCancelled = DateTime.Now;
            }
            else if (items.Any(i => i.Status == OrderItemStatus.Received))
            {
                order.Status = OrderStatus.PartiallyCompleted;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                "OrderDetails",
                new { id = orderId }
            );
        }


        [HttpPost]
        public async Task<IActionResult> RejectItem(int orderId, int orderItemId, string reason)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound();
            }

            var item = order.OrderItems
                .FirstOrDefault(i => i.OrderItemId == orderItemId);

            if (item == null)
            {
                return NotFound();
            }

            if (item.Status != OrderItemStatus.Pending)
            {
                return RedirectToAction(
                    "OrderDetails",
                    new { id = orderId }
                );
            }

            item.Status = OrderItemStatus.Cancelled;
            item.DateRejected = DateTime.Now;
            item.RejectReason = reason;

            var items = order.OrderItems.ToList();

            if (items.All(i => i.Status == OrderItemStatus.Received))
            {
                order.Status = OrderStatus.Completed;
                order.DateCompleted = DateTime.Now;
            }
            else if (items.All(i => i.Status == OrderItemStatus.Cancelled))
            {
                order.Status = OrderStatus.Cancelled;
                order.DateCancelled = DateTime.Now;
            }
            else if (items.Any(i => i.Status == OrderItemStatus.Received) &&
                     items.Any(i => i.Status == OrderItemStatus.Cancelled))
            {
                order.Status = OrderStatus.PartiallyCompleted;
            }
            else
            {
                order.Status = OrderStatus.Ordered;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                "OrderDetails",
                new { id = orderId }
            );
        }


        [HttpGet]
        public async Task<IActionResult> CreateOrder()
        {
            await LoadSuppliers();

            return View(new CreateOrderVM());
        }


        // =========================================================
        // CREATE ORDER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(CreateOrderVM vm)
        {
            // =====================================================
            // VALIDATE ITEMS
            // =====================================================

            if (vm.Items == null ||
                !vm.Items.Any(x => x.Quantity > 0))
            {
                ModelState.AddModelError(
                    "",
                    "Please enter at least one item quantity."
                );
            }


            // =====================================================
            // VALIDATE SUPPLIER
            // =====================================================

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.SupplierId == vm.SupplierID &&
                    s.IsActive);

            if (supplier == null)
            {
                ModelState.AddModelError(
                    "SupplierID",
                    "Please select a valid supplier."
                );
            }


            // =====================================================
            // RETURN IF BASIC VALIDATION FAILS
            // =====================================================

            if (!ModelState.IsValid)
            {
                await LoadSuppliers();

                return View(vm);
            }


            // =====================================================
            // GET SELECTED ITEMS
            // =====================================================

            var selectedItems = vm.Items
                .Where(x => x.Quantity > 0)
                .ToList();


            // =====================================================
            // GET VALID CONSUMABLES FROM DATABASE
            // =====================================================

            var consumableIds = selectedItems
                .Select(x => x.ConsumableID)
                .Distinct()
                .ToList();

            var consumables = await _context.Consumables
                .Where(c =>
                    consumableIds.Contains(c.ConsumableId) &&
                    c.SupplierId == vm.SupplierID &&
                    c.IsActive)
                .ToListAsync();


            // =====================================================
            // VALIDATE SELECTED CONSUMABLES
            // =====================================================

            foreach (var item in selectedItems)
            {
                var consumable = consumables
                    .FirstOrDefault(c =>
                        c.ConsumableId == item.ConsumableID);

                if (consumable == null)
                {
                    ModelState.AddModelError(
                        "",
                        $"Invalid consumable selected: {item.ConsumableID}"
                    );
                }
            }


            // Return if invalid consumables were selected
            if (!ModelState.IsValid)
            {
                await LoadSuppliers();

                return View(vm);
            }


            int year = DateTime.Now.Year;

            var yearlyOrderNumbers = await _context.Orders
                .Where(o => o.OrderDate.Year == year)
                .Select(o => o.OrderNumber)
                .ToListAsync();

            int nextNumber = 1;

            if (yearlyOrderNumbers.Any())
            {
                var numbers = yearlyOrderNumbers
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(orderNumber =>
                    {
                        var parts = orderNumber.Split('-');

                        if (parts.Length == 3 &&
                            int.TryParse(
                                parts[2],
                                out int number))
                        {
                            return number;
                        }

                        return 0;
                    })
                    .ToList();

                if (numbers.Any())
                {
                    nextNumber = numbers.Max() + 1;
                }
            }

            string orderNumber =
                $"ORD-{year}-{nextNumber:D3}";

            var order = new Order
            {
                OrderNumber = orderNumber,
                OrderDate = DateTime.Now,
                Status = OrderStatus.Ordered,
                SupplierId = vm.SupplierID,

                OrderItems = new List<OrderItem>()
            };

            foreach (var item in selectedItems)
            {
                var consumable = consumables
                    .First(c =>
                        c.ConsumableId == item.ConsumableID);

                var orderItem = new OrderItem
                {
                    ConsumableId = consumable.ConsumableId,
                    Quantity = item.Quantity,
                    Status = OrderItemStatus.Pending
                };

                order.OrderItems.Add(orderItem);
            }

            _context.Orders.Add(order);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Order {order.OrderNumber} created successfully.";


            return RedirectToAction(nameof(Orders));
        }


        [HttpGet]
        public async Task<IActionResult> GetConsumablesBySupplier(
            int supplierId)
        {
            var consumables = await _context.Consumables
                .Where(c =>
                    c.SupplierId == supplierId &&
                    c.IsActive)
                .OrderBy(c => c.ConsumableName)
                .Select(c => new
                {
                    consumableID = c.ConsumableId,
                    consumableName = c.ConsumableName,
                    quantityInHand = c.QuantityInHand,
                    reorderLevel = c.ReorderLevel,
                    status = c.Status.ToString()
                })
                .ToListAsync();

            return Json(consumables);
        }

        private async Task LoadSuppliers()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.SupplierName)
                .ToListAsync();

            ViewBag.Suppliers = new SelectList(
                suppliers,
                "SupplierId",
                "SupplierName"
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound();
            }

            // Prevent cancelling an already completed or cancelled order
            if (order.Status == OrderStatus.Completed ||
                order.Status == OrderStatus.Cancelled)
            {
                TempData["Error"] =
                    "This order cannot be cancelled.";

                return RedirectToAction(
                    nameof(OrderDetails),
                    new { id = orderId }
                );
            }

            var now = DateTime.Now;

            // Cancel all items that have not yet been received or cancelled
            foreach (var item in order.OrderItems)
            {
                if (item.Status == OrderItemStatus.Pending)
                {
                    item.Status = OrderItemStatus.Cancelled;
                    item.DateRejected = now;
                    item.RejectReason = "Order cancelled.";
                }
            }

            // Cancel the entire order
            order.Status = OrderStatus.Cancelled;
            order.DateCancelled = now;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Order {order.OrderNumber} and its pending items have been cancelled successfully.";

            return RedirectToAction(
                nameof(Orders));
        }
        #endregion

        #region Doctor

        public async Task<IActionResult> Doctors()
        {
            var doctor = await _context.Doctors
                .Include(x => x.User)
                .Where(d => d.IsActive)
                .OrderBy(x => x.Surname)
                .ThenBy(x => x.Name)
                .ToListAsync();

            ViewBag.FilterTitle = "Active Doctors";

            return View(doctor);
        }

        [HttpGet]
        public async Task<IActionResult> SearchDoctorsLive(
    string term = "",
    string status = "active")
        {
            var query = _context.Doctors
                .AsQueryable();

            // Filter by active/inactive status.
            if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(d => !d.IsActive);
            }
            else
            {
                query = query.Where(d => d.IsActive);
            }

            // Search by name, email, HPCSA number, or phone.
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(d =>
                    (d.Name != null && d.Name.Contains(term)) ||
                    (d.Surname != null && d.Surname.Contains(term)) ||
                    (d.User != null && d.User.Email != null &&
                        d.User.Email.Contains(term)) ||
                    (d.HPCSANumber != null && d.HPCSANumber.Contains(term)) ||
                    (d.PhoneNumber != null && d.PhoneNumber.Contains(term)));
            }

            var doctors = await query
                .OrderBy(d => d.Surname)
                .Select(d => new
                {
                    doctorId = d.DoctorId,
                    name = d.Name,
                    surname = d.Surname,
                    email = d.User != null ? d.User.Email : null,
                    hpcsaNumber = d.HPCSANumber,
                    phoneNumber = d.PhoneNumber,
                    isActive = d.IsActive
                })
                .ToListAsync();

            return Json(doctors);
        }
        public async Task<IActionResult> InactiveDoctors()
        {
            var doctor = await _context.Doctors
                .Include(x => x.User)
                .Where(d => !d.IsActive)
                .OrderBy(x => x.Surname)
                .ThenBy(x => x.Name)
                .ToListAsync();

            ViewBag.FilterTitle = "Inactive Doctors";
            return View(doctor);
        }

        [HttpGet]
        public IActionResult AddDoctor()
        {
            return View(new AddDoctorViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDoctor(AddDoctorViewModel model)
        {


            if (!ModelState.IsValid)
            {
                return View(model);
            }



            string email = model.Email.Trim();
            string phoneNumber = model.PhoneNumber.Trim();
            string name = model.Name.Trim();
            string surname = model.Surname.Trim();
            string hpcsaNumber = model.HPCSANumber.Trim();


            var existingUser = await _userManager
                .FindByEmailAsync(email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered."
                );

                return View(model);
            }


            bool hpcsaExists = await _context.Doctors
                .AnyAsync(d => d.HPCSANumber == hpcsaNumber);

            if (hpcsaExists)
            {
                ModelState.AddModelError(
                    nameof(model.HPCSANumber),
                    "This HPCSA number is already registered."
                );

                return View(model);
            }

            string temporaryPassword = GeneratePassword();



            var user = new Users
            {
                FullName = $"{name} {surname}",
                UserName = email,
                Email = email,
                PhoneNumber = phoneNumber,
                EmailConfirmed = true
            };


            var userResult = await _userManager
                .CreateAsync(user, temporaryPassword);


            if (!userResult.Succeeded)
            {
                foreach (var error in userResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description
                    );
                }

                return View(model);
            }

            var roleResult = await _userManager
                .AddToRoleAsync(user, "Doctor");


            if (!roleResult.Succeeded)
            {
                // Delete user if role assignment fails
                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description
                    );
                }

                return View(model);
            }


            var doctor = new Doctor
            {
                Name = name,
                Surname = surname,
                PhoneNumber = phoneNumber,
                HPCSANumber = hpcsaNumber,
                UserId = user.Id,
                IsActive = true
            };



            _context.Doctors.Add(doctor);

            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    email,
                    name,
                    surname,
                    temporaryPassword,
                    "Doctor"
                );
            }
            catch (Exception)
            {
                Console.WriteLine("Email not sent");
            }


            TempData["Success"] =
                $"Doctor {name} {surname} was added successfully and email with temporary password is sent"
                ;


            return RedirectToAction(nameof(Doctors));
        }


        private string GeneratePassword()
        {
            return "Doc@" +
                   Guid.NewGuid()
                       .ToString("N")
                       .Substring(0, 8);
        }


        [HttpGet]
        public async Task<IActionResult> EditDoctor(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor == null)
            {
                return NotFound();
            }

            var model = new EditDoctorViewModel
            {
                DoctorId = doctor.DoctorId,
                Name = doctor.Name,
                Surname = doctor.Surname,
                Email = doctor.User.Email,
                PhoneNumber = doctor.PhoneNumber,
                HPCSANumber = doctor.HPCSANumber,
                IsActive = doctor.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDoctor(EditDoctorViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DoctorId == model.DoctorId);

            if (doctor == null)
            {
                return NotFound();
            }

            // ============================
            // CHECK HPCSA NUMBER
            // ============================

            bool hpcsaExists = await _context.Doctors
                .AnyAsync(d =>
                    d.DoctorId != model.DoctorId &&
                    d.HPCSANumber == model.HPCSANumber);

            if (hpcsaExists)
            {
                ModelState.AddModelError(
                    nameof(model.HPCSANumber),
                    "This HPCSA number is already registered."
                );

                return View(model);
            }

            // ============================
            // CHECK EMAIL
            // ============================

            var existingUser = await _userManager
                .FindByEmailAsync(model.Email);

            if (existingUser != null &&
                existingUser.Id != doctor.UserId)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered."
                );

                return View(model);
            }

      

            doctor.Name = model.Name;
            doctor.Surname = model.Surname;
            doctor.PhoneNumber = model.PhoneNumber;
            doctor.HPCSANumber = model.HPCSANumber;
         

            if (doctor.User != null)
            {
                doctor.User.FullName =
                    $"{model.Name} {model.Surname}";

                doctor.User.Email = model.Email;
                doctor.User.UserName = model.Email;
                doctor.User.PhoneNumber = model.PhoneNumber;

                var updateUserResult =
                    await _userManager.UpdateAsync(doctor.User);

                if (!updateUserResult.Succeeded)
                {
                    foreach (var error in updateUserResult.Errors)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            error.Description
                        );
                    }

                    return View(model);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Doctor {model.Name} {model.Surname} updated successfully.";

            return RedirectToAction(nameof(Doctors));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor == null)
            {
                return NotFound();
            }

            // Soft delete
            doctor.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Dr. {doctor.Name} {doctor.Surname} has been deactivated successfully.";

            return RedirectToAction(nameof(Doctors));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateDoctor(int id)
        {
            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor == null)
            {
                return NotFound();
            }

            // Soft delete
            doctor.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Dr. {doctor.Name} {doctor.Surname} has been activated successfully.";

            return RedirectToAction(nameof(InactiveDoctors));
        }


        public async Task<IActionResult> DoctorDetails(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor == null)
                return NotFound();

            return View(doctor);
        }
        #endregion

        #region LabTechnician

        public async Task<IActionResult> LabTechnician()
        {
            var technicians = await _context.LabTechnicians
                .Include(x => x.User)
                .Include(x => x.TechnicianTests)
                    .ThenInclude(tt => tt.TestType)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Surname)
                .ThenBy(x => x.Name)
                .ToListAsync();

            ViewBag.TechnicianCount = technicians.Count;

            return View(technicians);
        }

        public async Task<IActionResult> InactiveLabTechnician()
        {
            var technicians = await _context.LabTechnicians
                .Include(x => x.User)
                .Include(x => x.TechnicianTests)
                    .ThenInclude(tt => tt.TestType)
                .Where(x => !x.IsActive)
                .OrderBy(x => x.Surname)
                .ThenBy(x => x.Name)
                .ToListAsync();

            ViewBag.TechnicianCount = technicians.Count;

            return View(technicians);
        }

        public async Task<IActionResult> TechnicianDetails(int id)
        {
            var technician = await _context.LabTechnicians
                .Include(t => t.User)
                .Include(t => t.TechnicianTests)
                    .ThenInclude(tt => tt.TestType)
                .FirstOrDefaultAsync(t => t.LabTechnicianId == id);

            if (technician == null)
            {
                TempData["Error"] = "Lab technician not found.";
                return RedirectToAction(nameof(LabTechnician));
            }

            return View(technician);
        }
        // GET: Manager/AddTechnician
        [HttpGet]
        public async Task<IActionResult> AddTechnician()
        {
            var model = new TechnicianViewModel
            {
                TestTypes = await _context.TestTypes
                    .Where(t => t.IsActive)
                    .Select(t => new TestTypeViewModel
                    {
                        TestTypeId = t.TestTypeId,
                        TestTypeName = t.TestTypeName
                    })
                    .ToListAsync()
            };

            return View(model);
        }


        // POST: Manager/AddTechnician
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTechnician(
            TechnicianViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.TestTypes = await _context.TestTypes
                    .Where(t => t.IsActive)
                    .Select(t => new TestTypeViewModel
                    {
                        TestTypeId = t.TestTypeId,
                        TestTypeName = t.TestTypeName
                    })
                    .ToListAsync();

                return View(model);
            }

            // =========================
            // CLEAN INPUT
            // =========================

            string email = model.Email.Trim();
            string phoneNumber = model.PhoneNumber.Trim();
            string name = model.Name.Trim();
            string surname = model.Surname.Trim();
            string idNumber = model.IDNumber.Trim();

            // =========================
            // CHECK EMAIL
            // =========================

            var existingUser = await _userManager
                .FindByEmailAsync(email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered.");

                await LoadTechnicianTestTypes(model);

                return View(model);
            }

            // =========================
            // CHECK ID NUMBER
            // =========================

            bool idExists = await _context.LabTechnicians
                .AnyAsync(t => t.IDNumber == idNumber);

            if (idExists)
            {
                ModelState.AddModelError(
                    nameof(model.IDNumber),
                    "This ID number is already registered.");

                await LoadTechnicianTestTypes(model);

                return View(model);
            }


            int year = DateTime.Now.Year;
            string prefix = $"EMP-{year}-";

            var lastEmployeeNumber = await _context.LabTechnicians
                .Where(t => t.EmployeeNumber.StartsWith(prefix))
                .OrderByDescending(t => t.EmployeeNumber)
                .Select(t => t.EmployeeNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrEmpty(lastEmployeeNumber))
            {
                string numberPart = lastEmployeeNumber.Substring(prefix.Length);

                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            string employeeNumber = $"{prefix}{nextNumber:D3}";


            string temporaryPassword = GenerateTechnicianPassword();

            // =========================
            // CREATE IDENTITY USER
            // =========================

            var user = new Users
            {
                FullName = $"{name} {surname}",
                UserName = email,
                Email = email,
                PhoneNumber = phoneNumber,
                EmailConfirmed = true
            };

            var userResult = await _userManager
                .CreateAsync(user, temporaryPassword);

            if (!userResult.Succeeded)
            {
                foreach (var error in userResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                await LoadTechnicianTestTypes(model);

                return View(model);
            }

            // =========================
            // ASSIGN TECHNICIAN ROLE
            // =========================

            var roleResult = await _userManager
                .AddToRoleAsync(user, "LabTechnician");

            if (!roleResult.Succeeded)
            {
                // Remove user if role assignment fails
                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                await LoadTechnicianTestTypes(model);

                return View(model);
            }

            // =========================
            // CREATE LAB TECHNICIAN
            // =========================

            var technician = new LabTechnician
            {
                Name = name,
                Surname = surname,
                IDNumber = idNumber,
                PhoneNumber = phoneNumber,
                EmployeeNumber = employeeNumber,
                IsActive = true,

                // Link technician to Identity user
                UserId = user.Id
            };

            _context.LabTechnicians.Add(technician);

            await _context.SaveChangesAsync();

            // =========================
            // ASSIGN TEST TYPES
            // =========================

            if (model.SelectedTestTypeIds != null &&
                model.SelectedTestTypeIds.Any())
            {
                foreach (var testTypeId in model.SelectedTestTypeIds)
                {
                    var technicianTest = new TechnicianTestType
                    {
                        LabTechnicianId = technician.LabTechnicianId,
                        TestTypeId = testTypeId
                    };

                    _context.TechnicianTestTypes.Add(technicianTest);
                }

                await _context.SaveChangesAsync();
            }

            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    email,
                    name,
                    surname,
                    temporaryPassword,
                    "LabTechnician"
                );
            }
            catch (Exception)
            {
                Console.WriteLine("Email not sent");
            }

            TempData["Success"] =
                $"Technician {name} {surname} was added successfully and email with temporary password is sent";

            return RedirectToAction(nameof(LabTechnician));
        }

        private string GenerateTechnicianPassword()
        {
            return "Tech@" +
                   Guid.NewGuid()
                       .ToString("N")
                       .Substring(0, 8);
        }


        private async Task LoadTechnicianTestTypes(
            TechnicianViewModel model)
        {
            model.TestTypes = await _context.TestTypes
                .Where(t => t.IsActive)
                .Select(t => new TestTypeViewModel
                {
                    TestTypeId = t.TestTypeId,
                    TestTypeName = t.TestTypeName
                })
                .ToListAsync();
        }

        // GET: Manager/EditTechnician/5
        [HttpGet]
        public async Task<IActionResult> EditTechnician(int id)
        {
            var technician = await _context.LabTechnicians
                .Include(t => t.User)
                .Include(t => t.TechnicianTests)
                .FirstOrDefaultAsync(t => t.LabTechnicianId == id);

            if (technician == null)
            {
                return NotFound();
            }

            var model = new TechnicianViewModel
            {
                LabTechnicianId = technician.LabTechnicianId,
                Name = technician.Name,
                Surname = technician.Surname,
                IDNumber = technician.IDNumber,
                EmployeeNumber = technician.EmployeeNumber,
                Email = technician.User?.Email ?? string.Empty,
                PhoneNumber = technician.PhoneNumber,

                SelectedTestTypeIds = technician.TechnicianTests
                    .Select(tt => tt.TestTypeId)
                    .ToList()
            };

            await LoadTechnicianTestTypes(model);

            return View(model);
        }


        // POST: Manager/EditTechnician/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTechnician(
     int id,
     TechnicianViewModel model)
        {
            if (id != model.LabTechnicianId)
            {
                return BadRequest();
            }

            // ==========================================
            // LOAD TECHNICIAN
            // ==========================================

            var technician = await _context.LabTechnicians
                .Include(t => t.User)
                .Include(t => t.TechnicianTests)
                .FirstOrDefaultAsync(t => t.LabTechnicianId == id);

            if (technician == null)
            {
                return NotFound();
            }

            // ==========================================
            // CLEAN INPUT
            // ==========================================

            string name = model.Name?.Trim() ?? "";
            string surname = model.Surname?.Trim() ?? "";
            string idNumber = model.IDNumber?.Trim() ?? "";
            string email = model.Email?.Trim() ?? "";
            string phoneNumber = model.PhoneNumber?.Trim() ?? "";

            // ==========================================
            // CHECK MODEL VALIDATION
            // ==========================================

            if (!ModelState.IsValid)
            {
                await LoadTechnicianTestTypes(model);
                return View(model);
            }

            // ==========================================
            // CHECK ID NUMBER
            // ==========================================

            bool idExists = await _context.LabTechnicians
                .AnyAsync(t =>
                    t.IDNumber == idNumber &&
                    t.LabTechnicianId != technician.LabTechnicianId);

            if (idExists)
            {
                ModelState.AddModelError(
                    nameof(model.IDNumber),
                    "This ID number is already registered.");

                await LoadTechnicianTestTypes(model);
                return View(model);
            }

            // ==========================================
            // CHECK EMAIL
            // ==========================================

            if (technician.User != null)
            {
                // Search by normalized email
                string normalizedEmail =
                    _userManager.NormalizeEmail(email);

                var existingUser = await _userManager.Users
                    .FirstOrDefaultAsync(u =>
                        u.NormalizedEmail == normalizedEmail &&
                        u.Id != technician.User.Id);

                if (existingUser != null)
                {
                    ModelState.AddModelError(
                        nameof(model.Email),
                        "This email is already registered.");

                    await LoadTechnicianTestTypes(model);
                    return View(model);
                }
            }

            // ==========================================
            // UPDATE TECHNICIAN
            // ==========================================

            technician.Name = name;
            technician.Surname = surname;
            technician.IDNumber = idNumber;
            technician.PhoneNumber = phoneNumber;

            // EmployeeNumber remains unchanged
            // IsActive remains unchanged

            // ==========================================
            // UPDATE IDENTITY USER
            // ==========================================

            if (technician.User != null)
            {
                technician.User.FullName =
                    $"{name} {surname}";

                technician.User.Email = email;
                technician.User.UserName = email;
                technician.User.PhoneNumber = phoneNumber;

                var userUpdateResult =
                    await _userManager.UpdateAsync(technician.User);

                if (!userUpdateResult.Succeeded)
                {
                    foreach (var error in userUpdateResult.Errors)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            error.Description);
                    }

                    await LoadTechnicianTestTypes(model);
                    return View(model);
                }
            }

            // ==========================================
            // UPDATE TEST TYPE ASSIGNMENTS
            // ==========================================

            var selectedTestTypeIds =
                model.SelectedTestTypeIds?
                    .Distinct()
                    .ToList()
                ?? new List<int>();

            // Only allow active test types
            var validTestTypeIds =
                await _context.TestTypes
                    .Where(t =>
                        t.IsActive &&
                        selectedTestTypeIds.Contains(t.TestTypeId))
                    .Select(t => t.TestTypeId)
                    .ToListAsync();

            // Remove existing assignments
            _context.TechnicianTestTypes.RemoveRange(
                technician.TechnicianTests);

            // Add new assignments
            foreach (var testTypeId in validTestTypeIds)
            {
                _context.TechnicianTestTypes.Add(
                    new TechnicianTestType
                    {
                        LabTechnicianId =
                            technician.LabTechnicianId,

                        TestTypeId = testTypeId
                    });
            }

            // ==========================================
            // SAVE EVERYTHING
            // ==========================================

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Technician {name} {surname} was updated successfully.";

            return RedirectToAction(nameof(LabTechnician));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTechnician(int id)
        {
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.LabTechnicianId == id);

            if (technician == null)
            {
                return NotFound();
            }

            // Get test types assigned to this technician
            var assignedTestTypes = await _context.TechnicianTestTypes
                .Where(tt => tt.LabTechnicianId == id)
                .Select(tt => new
                {
                    tt.TestTypeId,
                    tt.TestType.TestTypeName
                })
                .ToListAsync();

            // Find test types without another active technician
            foreach (var testType in assignedTestTypes)
            {
                var hasOtherActiveTechnician =
                    await _context.TechnicianTestTypes
                        .AnyAsync(tt =>
                            tt.TestTypeId == testType.TestTypeId &&
                            tt.LabTechnicianId != id &&
                            tt.LabTechnician.IsActive);

                if (!hasOtherActiveTechnician)
                {
                    TempData["Warning"] =
                        $"The test type '{testType.TestTypeName}' " +
                        "needs another active technician before this " +
                        "technician can be deactivated.";

                    // Redirect to the reassignment page for this test type
                    return RedirectToAction(
                        "ReassignView",
                        new
                        {
                            testTypeId = testType.TestTypeId,
                            technicianId = id
                        });
                }
            }

            // All test types have another active technician
            technician.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Technician {technician.Name} {technician.Surname} " +
                "has been deactivated successfully.";

            return RedirectToAction(nameof(LabTechnician));
        }


        [HttpGet]
        public async Task<IActionResult> ReassignTechnician(
            int testTypeId, int technicianId)
        {
            var testType = await _context.TestTypes
                .FirstOrDefaultAsync(t => t.TestTypeId == testTypeId);

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.LabTechnicianId == technicianId);

            if (testType == null || technician == null)
            {
                return NotFound();
            }

            ViewBag.TestTypeId = testTypeId;
            ViewBag.TestTypeName = testType.TestTypeName;
            ViewBag.TechnicianId = technicianId;
            ViewBag.TechnicianName =
                $"{technician.Name} {technician.Surname}";

            // Only active technicians who are not the technician
            // being deactivated and are not already assigned
            var availableTechnicians = await _context.LabTechnicians
                .Where(t => t.IsActive && t.LabTechnicianId != technicianId)
                .Where(t => !_context.TechnicianTestTypes.Any(tt =>
                    tt.TestTypeId == testTypeId &&
                    tt.LabTechnicianId == t.LabTechnicianId))
                .ToListAsync();

            return View(availableTechnicians);
        }



        [HttpGet]
        public async Task<IActionResult> ReassignView(int technicianId)
        {
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t =>
                    t.LabTechnicianId == technicianId);

            if (technician == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction("LabTechnician");
            }

            // Get ALL test types for this technician that have
            // no other active technician assigned.
            var testTypes = await _context.TechnicianTestTypes
                .Where(x => x.LabTechnicianId == technicianId)
                .Where(x => !_context.TechnicianTestTypes.Any(other =>
                    other.TestTypeId == x.TestTypeId &&
                    other.LabTechnicianId != technicianId &&
                    other.LabTechnician.IsActive))
                .Select(x => new
                {
                    x.TestTypeId,
                    TestTypeName = x.TestType.TestTypeName
                })
                .Distinct()
                .ToListAsync();

            var viewModel =
                new HemaCore.ViewModels.ManagerVM.ReassignTechnicianViewModel
                {
                    TechnicianId = technician.LabTechnicianId,
                    TechnicianName =
                        $"{technician.Name} {technician.Surname}"
                };

            // Build a separate replacement list for every test type.
            foreach (var testType in testTypes)
            {
                var alreadyAssignedIds = await _context.TechnicianTestTypes
                    .Where(x => x.TestTypeId == testType.TestTypeId)
                    .Select(x => x.LabTechnicianId)
                    .ToListAsync();

                var availableTechnicians = await _context.LabTechnicians
                    .Where(t =>
                        t.IsActive &&
                        t.LabTechnicianId != technicianId &&
                        !alreadyAssignedIds.Contains(t.LabTechnicianId))
                    .OrderBy(t => t.Name)
                    .ThenBy(t => t.Surname)
                    .ToListAsync();

                viewModel.TestTypes.Add(
                    new HemaCore.ViewModels.ManagerVM.ReassignTestTypeViewModel
                    {
                        TestTypeId = testType.TestTypeId,
                        TestTypeName = testType.TestTypeName,
                        AvailableTechnicians = availableTechnicians
                    });
            }

            return View("ReassignView", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReassignTechnician(
     int testTypeId,
     int technicianId,
     int newTechnicianId)
        {
            if (technicianId == newTechnicianId)
            {
                TempData["Error"] = "Select a different technician.";

                return RedirectToAction(
                    "ReassignView",
                    new { technicianId });
            }

            var oldTechnician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t =>
                    t.LabTechnicianId == technicianId);

            var newTechnician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t =>
                    t.LabTechnicianId == newTechnicianId &&
                    t.IsActive);

            if (oldTechnician == null || newTechnician == null)
            {
                TempData["Error"] =
                    "The original technician or active replacement technician could not be found.";

                return RedirectToAction(
                    "ReassignView",
                    new { technicianId });
            }

            var assignment = await _context.TechnicianTestTypes
                .FirstOrDefaultAsync(x =>
                    x.TestTypeId == testTypeId &&
                    x.LabTechnicianId == technicianId);

            if (assignment == null)
            {
                TempData["Error"] =
                    "The test type assignment was not found.";

                return RedirectToAction(
                    "ReassignView",
                    new { technicianId });
            }

            var alreadyAssigned = await _context.TechnicianTestTypes
                .AnyAsync(x =>
                    x.TestTypeId == testTypeId &&
                    x.LabTechnicianId == newTechnicianId);

            if (alreadyAssigned)
            {
                TempData["Error"] =
                    "The replacement technician is already assigned to this test type.";

                return RedirectToAction(
                    "ReassignView",
                    new { technicianId });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Transfer the selected test type.
                _context.TechnicianTestTypes.Remove(assignment);

                _context.TechnicianTestTypes.Add(
                    new TechnicianTestType
                    {
                        TestTypeId = testTypeId,
                        LabTechnicianId = newTechnicianId
                    });

                await _context.SaveChangesAsync();

                // Check whether the original technician still has
                // ANY test type assignments.
                var remainingAssignments = await _context
                    .TechnicianTestTypes
                    .AnyAsync(x =>
                        x.LabTechnicianId == technicianId);

                if (remainingAssignments)
                {
                    // Keep the technician active until all assignments
                    // have been transferred.
                    oldTechnician.IsActive = true;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] =
                        "Test type reassigned successfully. " +
                        "The technician remains active because other " +
                        "test type assignments still need to be reassigned.";

                    return RedirectToAction(
                        "ReassignView",
                        new { technicianId });
                }

                // No assignments remain, so the technician can
                // safely be deactivated.
                oldTechnician.IsActive = false;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "All test type assignments have been reassigned. " +
                    "The original technician has been deactivated.";

                return RedirectToAction("LabTechnician");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchTechniciansLive(
    string term = "",
    string status = "active")
        {
            var query = _context.LabTechnicians
                .AsQueryable();

            // Active or inactive technicians.
            if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => !t.IsActive);
            }
            else
            {
                query = query.Where(t => t.IsActive);
            }

            // Search technician details.
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();

                query = query.Where(t =>
                    (t.Name != null && t.Name.Contains(term)) ||
                    (t.Surname != null && t.Surname.Contains(term)) ||
                    (t.IDNumber != null && t.IDNumber.Contains(term)) ||
                    (t.EmployeeNumber != null && t.EmployeeNumber.Contains(term)) ||
                    (t.PhoneNumber != null && t.PhoneNumber.Contains(term)) ||
                    (t.User != null &&
                     t.User.Email != null &&
                     t.User.Email.Contains(term)) ||
                    t.TechnicianTests.Any(a =>
                        a.TestType != null &&
                        a.TestType.TestTypeName.Contains(term)));
            }

            var technicians = await query
                .OrderBy(t => t.Surname)
                .Select(t => new
                {
                    labTechnicianId = t.LabTechnicianId,
                    name = t.Name,
                    surname = t.Surname,
                    idNumber = t.IDNumber,
                    employeeNumber = t.EmployeeNumber,
                    email = t.User != null ? t.User.Email : null,
                    phoneNumber = t.PhoneNumber,
                    isActive = t.IsActive,

                    testTypes = t.TechnicianTests
                        .Where(a => a.TestType != null)
                        .Select(a => new
                        {
                            testTypeName = a.TestType!.TestTypeName
                        })
                        .ToList()
                })
                .ToListAsync();

            return Json(technicians);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateTechnician(int id)
        {
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.LabTechnicianId == id);

            if (technician == null)
            {
                return NotFound();
            }


            technician.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Technician {technician.Name} {technician.Surname} has been activated successfully.";

            return RedirectToAction(nameof(InactiveLabTechnician));
        }
        #endregion

        #region Reports


        [HttpGet]
        public IActionResult TestsPerformedReport()
        {
            return View(new TestsPerformedReportViewModel
            {
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today
            });
        }




        public async Task<IActionResult> GenerateTestsByCategoryReport(
            DateTime startDate,
            DateTime endDate)
        {
            // Validate the selected dates
            if (startDate.Date > endDate.Date)
            {
                TempData["Error"] = "The start date cannot be after the end date.";
                return RedirectToAction("Index");
            }

            var startDateTime = startDate.Date;
            var endDateTime = endDate.Date.AddDays(1);
            var generatedAt = DateTime.Now;

            // Get verified tests completed within the selected period
            var performedTests = await (
                from item in _context.TestRequestTestTypes
                join type in _context.TestTypes
                    on item.TestTypeId equals type.TestTypeId
                join category in _context.TestCategories
                    on type.CategoryId equals category.CategoryId
                where item.CompletionDateTime.HasValue
                    && item.CompletionDateTime.Value >= startDateTime
                    && item.CompletionDateTime.Value < endDateTime
                    && item.TestResultId.HasValue
                    && item.Status == "Verified"
                select new
                {
                    CategoryName = category.CategoryName,
                    TestTypeName = type.TestTypeName
                }
            ).ToListAsync();

            // Group tests by category, then by test type
            var groupedTests = performedTests
                .GroupBy(x => x.CategoryName ?? "Uncategorised")
                .OrderBy(g => g.Key)
                .Select(category => new
                {
                    CategoryName = category.Key,

                    TestTypes = category
                        .GroupBy(x => x.TestTypeName ?? "Unnamed Test")
                        .OrderBy(g => g.Key)
                        .Select(testType => new
                        {
                            TestTypeName = testType.Key,
                            Count = testType.Count()
                        })
                        .ToList(),

                    CategoryTotal = category.Count()
                })
                .ToList();

            var grandTotal = performedTests.Count;

            // Create the PDF document
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(style =>
                        style.FontFamily("Lato").FontSize(10));

                    // =========================
                    // HEADER
                    // =========================
                    page.Header().Column(header =>
                    {
                        header.Item()
                            .Background(Colors.Blue.Darken3)
                            .Padding(18)
                            .Column(column =>
                            {
                                column.Item()
                                    .Text("HemaCore")
                                    .FontSize(24)
                                    .Bold()
                                    .FontColor(Colors.White);

                                column.Item()
                                    .Text("LABORATORY MANAGEMENT SYSTEM")
                                    .FontSize(9)
                                    .FontColor(Colors.White);
                            });

                        header.Item()
                            .PaddingTop(15)
                            .Text("TESTS PERFORMED BY CATEGORY")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        header.Item()
                            .PaddingTop(4)
                            .Text("Management Report")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    // =========================
                    // REPORT CONTENT
                    // =========================
                    page.Content()
                        .PaddingVertical(15)
                        .Column(content =>
                        {
                            content.Spacing(12);

                            // REPORT PERIOD
                            content.Item()
                                .Border(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(12)
                                .Column(column =>
                                {
                                    column.Spacing(5);

                                    column.Item()
                                        .Text("REPORT PERIOD")
                                        .FontSize(9)
                                        .Bold()
                                        .FontColor(Colors.Blue.Darken3);

                                    column.Item()
                                        .Text(
                                            $"{startDate:dd MMM yyyy} - " +
                                            $"{endDate:dd MMM yyyy}")
                                        .FontSize(11)
                                        .Bold();

                                    column.Item()
                                        .Text(
                                            $"Generated on: {generatedAt:dd MMM yyyy HH:mm}")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken1);
                                });

                            // SUMMARY CARDS
                            content.Item().Row(row =>
                            {
                                row.RelativeItem()
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(12)
                                    .Column(column =>
                                    {
                                        column.Item()
                                            .Text("TOTAL TESTS PERFORMED")
                                            .FontSize(9)
                                            .FontColor(Colors.Grey.Darken1);

                                        column.Item()
                                            .Text(grandTotal.ToString())
                                            .FontSize(22)
                                            .Bold()
                                            .FontColor(Colors.Blue.Darken3);
                                    });

                                row.ConstantItem(12);

                                row.RelativeItem()
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(12)
                                    .Column(column =>
                                    {
                                        column.Item()
                                            .Text("TEST CATEGORIES")
                                            .FontSize(9)
                                            .FontColor(Colors.Grey.Darken1);

                                        column.Item()
                                            .Text(groupedTests.Count.ToString())
                                            .FontSize(22)
                                            .Bold()
                                            .FontColor(Colors.Blue.Darken3);
                                    });
                            });

                            // =========================
                            // TEST CATEGORY AND TEST TYPE TABLE
                            // =========================
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                });

                                // TABLE HEADERS
                                table.Header(header =>
                                {
                                    header.Cell()
                                        .Background(Colors.Blue.Darken3)
                                        .Padding(10)
                                        .Text("")
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Background(Colors.Blue.Darken3)
                                        .Padding(10)
                                        .AlignRight()
                                        .Text("NUMBER PERFORMED")
                                        .Bold()
                                        .FontColor(Colors.White);
                                });

                                // NO RESULTS MESSAGE
                                if (groupedTests.Count == 0)
                                {
                                    table.Cell()
                                        .ColumnSpan(2)
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Padding(12)
                                        .Text(
                                            "No verified tests were found for the selected period.")
                                        .FontColor(Colors.Grey.Darken1);
                                }

                                // LOOP THROUGH TEST CATEGORIES
                                foreach (var category in groupedTests)
                                {
                                    // CATEGORY HEADING
                                    table.Cell()
                                        .ColumnSpan(2)
                                        .Background(Colors.Blue.Lighten3)
                                        .Padding(10)
                                        .Text(text =>
                                        {
                                            text.Span("TEST CATEGORY: ")
                                                .Bold()
                                                .FontColor(Colors.Blue.Darken4);

                                            text.Span(category.CategoryName)
                                                .Bold()
                                                .FontColor(Colors.Blue.Darken4);
                                        });

                                    // LOOP THROUGH TEST TYPES IN THIS CATEGORY
                                    foreach (var test in category.TestTypes)
                                    {
                                        // TEST TYPE NAME
                                        table.Cell()
                                            .BorderBottom(1)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(10)
                                            .PaddingLeft(20)
                                            .Text(text =>
                                            {
                                                text.Span(" ")
                                                    .FontColor(Colors.Grey.Darken1);

                                                text.Span(test.TestTypeName)
                                                    .Bold();
                                            });

                                        // NUMBER OF TESTS PERFORMED
                                        table.Cell()
                                            .BorderBottom(1)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(10)
                                            .AlignRight()
                                            .Text(test.Count.ToString());
                                    }

                                    // CATEGORY SUBTOTAL
                                    table.Cell()
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(10)
                                        .Text(
                                            $"TOTAL FOR {category.CategoryName.ToUpper()}")
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(10)
                                        .AlignRight()
                                        .Text(category.CategoryTotal.ToString())
                                        .Bold();
                                }

                                // GRAND TOTAL
                                table.Cell()
                                    .Background(Colors.Blue.Darken3)
                                    .Padding(11)
                                    .Text("GRAND TOTAL - ALL CATEGORIES")
                                    .Bold()
                                    .FontColor(Colors.White);

                                table.Cell()
                                    .Background(Colors.Blue.Darken3)
                                    .Padding(11)
                                    .AlignRight()
                                    .Text(grandTotal.ToString())
                                    .Bold()
                                    .FontColor(Colors.White);
                            });

                            // EXPLANATION
                           
                        });

                    // =========================
                    // FOOTER
                    // =========================
                    page.Footer()
                        .PaddingTop(8)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text("HemaCore | Tests Performed Report")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);

                            row.ConstantItem(100)
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.Span("Page ");
                                    text.CurrentPageNumber();
                                    text.Span(" of ");
                                    text.TotalPages();
                                });
                        });
                });
            });

            // Generate and download the PDF
            var pdfBytes = document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"Tests_Performed_By_Category_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }




        #endregion

        #region Manager Profile


        [HttpGet]
        public async Task<IActionResult> ManagerProfile()
        {
            // Always load the currently signed-in manager.
            var manager = await _userManager.GetUserAsync(User);

            if (manager == null ||
                !await _userManager.IsInRoleAsync(manager, "Manager"))
            {
                TempData["Error"] = "Manager account was not found.";
                return RedirectToAction(nameof(Index));
            }

            var model = new ManagerProfileVM
            {
                Id = manager.Id,
                FullName = manager.FullName ?? string.Empty,
                Email = manager.Email ?? string.Empty,
                PhoneNumber = manager.PhoneNumber ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerProfile(ManagerProfileVM model)
        {
            // Get the authenticated manager rather than trusting the posted ID.
            var manager = await _userManager.GetUserAsync(User);

            if (manager == null ||
                !await _userManager.IsInRoleAsync(manager, "Manager"))
            {
                TempData["Error"] = "Manager account was not found.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent editing another user's account by changing the hidden ID.
            if (!string.Equals(model.Id, manager.Id, StringComparison.Ordinal))
            {
                return Forbid();
            }

            bool changingPassword =
                !string.IsNullOrWhiteSpace(model.NewPassword);

            // Password fields are optional when the password is unchanged.
            if (!changingPassword)
            {
                ModelState.Remove(nameof(model.CurrentPassword));
                ModelState.Remove(nameof(model.NewPassword));
                ModelState.Remove(nameof(model.ConfirmNewPassword));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    ModelState.AddModelError(
                        nameof(model.CurrentPassword),
                        "Enter your current password.");
                }

                if (string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                {
                    ModelState.AddModelError(
                        nameof(model.ConfirmNewPassword),
                        "Confirm your new password.");
                }
                else if (model.NewPassword != model.ConfirmNewPassword)
                {
                    ModelState.AddModelError(
                        nameof(model.ConfirmNewPassword),
                        "The new passwords do not match.");
                }
            }

            // Normalize values before validating or saving.
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Ensure another account is not using this email.
            var existingUser = await _userManager.FindByEmailAsync(model.Email);

            if (existingUser != null && existingUser.Id != manager.Id)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email address is already in use.");

                return View(model);
            }

            // Check the current password before making profile changes.
            if (changingPassword)
            {
                var passwordIsCorrect = await _userManager.CheckPasswordAsync(
                    manager, model.CurrentPassword!);

                if (!passwordIsCorrect)
                {
                    ModelState.AddModelError(
                        nameof(model.CurrentPassword),
                        "Your current password is incorrect.");

                    return View(model);
                }
            }

            string oldEmail = manager.Email ?? string.Empty;
            string oldUserName = manager.UserName ?? string.Empty;
            string newEmail = model.Email;

            bool emailChanged = !string.Equals(
                oldEmail, newEmail, StringComparison.OrdinalIgnoreCase);

            bool userNameChanged = !string.Equals(
                oldUserName, newEmail, StringComparison.OrdinalIgnoreCase);

            // Update the username first if the application uses email for login.
            if (userNameChanged)
            {
                var userNameResult = await _userManager.SetUserNameAsync(
                    manager, newEmail);

                if (!userNameResult.Succeeded)
                {
                    foreach (var error in userNameResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.Email), error.Description);
                    }

                    return View(model);
                }
            }

            // Update the email through Identity, not just the view model.
            if (emailChanged)
            {
                var emailResult = await _userManager.SetEmailAsync(
                    manager, newEmail);

                if (!emailResult.Succeeded)
                {
                    // Attempt to restore the original username if the email update fails.
                    if (userNameChanged)
                    {
                        await _userManager.SetUserNameAsync(manager, oldUserName);
                    }

                    foreach (var error in emailResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.Email), error.Description);
                    }

                    return View(model);
                }
            }

            // Update the remaining profile properties.
            manager.FullName = model.FullName;
            manager.PhoneNumber = model.PhoneNumber;

            var profileResult = await _userManager.UpdateAsync(manager);

            if (!profileResult.Succeeded)
            {
                foreach (var error in profileResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            // Change the password only when requested.
            if (changingPassword)
            {
                var passwordResult = await _userManager.ChangePasswordAsync(
                    manager,
                    model.CurrentPassword!,
                    model.NewPassword!);

                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.NewPassword), error.Description);
                    }

                    return View(model);
                }
            }

            // Refresh the current manager's authentication cookie.
            await _signInManager.RefreshSignInAsync(manager);

            TempData["Success"] = changingPassword
                ? "Profile and password updated successfully."
                : "Profile updated successfully.";

            return RedirectToAction(nameof(ManagerDetails));
        }

        [HttpGet]
        public async Task<IActionResult> ManagerDetails()
        {
            var manager = await _userManager.GetUserAsync(User);

            if (manager == null ||
                !await _userManager.IsInRoleAsync(manager, "Manager"))
            {
                TempData["Error"] = "Manager account was not found.";
                return RedirectToAction(nameof(Index));
            }

            var model = new ManagerProfileVM
            {
                Id = manager.Id,
                FullName = manager.FullName ?? string.Empty,
                Email = manager.Email ?? string.Empty,
                PhoneNumber = manager.PhoneNumber ?? string.Empty
            };

            return View(model);
        }

        #endregion
    }
}


