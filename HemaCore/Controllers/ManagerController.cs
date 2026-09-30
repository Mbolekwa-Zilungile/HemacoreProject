using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.Enums;
using HemaCore.Services;
using HemaCore.ViewModels.ManagerVM;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Twilio.Rest.Trunking.V1;

namespace HemaCore.Controllers
{
    public class ManagerController : Controller
    {
        private AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IEmailService _emailService;

        public ManagerController(AppDbContext context, UserManager<Users> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }
        public IActionResult Index()
        {
            return View();
        }

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

        public IActionResult AddCategory()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>AddCategory(TestCategory category)
        {
            if (!ModelState.IsValid)
            {
                return View(category);
            }

            var existingCategory = await _context.TestCategories.FirstOrDefaultAsync(x => x.CategoryName == category.CategoryName);
            
            if(existingCategory != null)
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

            var category = await _context.TestCategories.FirstOrDefaultAsync(c => c.CategoryId == updatedCategory.CategoryId);
            if(category == null)
            {
                return NotFound();
            }

            category.CategoryName = updatedCategory.CategoryName;
            category.CategoryDescription = updatedCategory.CategoryDescription;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Category updated successfully.";

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
                .Any(t => t.CategoryId== id);

            if (hasTestTypes)
            {
                TempData["Error"] =
                    $"Cannot delete {category.CategoryName} because it has test types assigned to it.";

                return RedirectToAction("TestCategories");
            }

            category.IsActive = false;
            _context.SaveChanges();

            TempData["Success"] = $"{category.CategoryName} deleted successfully.";

            return RedirectToAction("TestCategories");
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
                .Include(t => t.TestTypeConsumables)
                .FirstOrDefaultAsync(t => t.TestTypeId == id);

            if (testType == null)
            {
                return NotFound();
            }
            bool hasTechnicians = testType.TechnicianTestType.Any();

            bool hasConsumables = testType.TestTypeConsumables.Any();

            if (hasTechnicians || hasConsumables)
            {
                TempData["Error"] =
                    "Cannot delete this Test Type because it is linked to technicians or consumables.";

                return RedirectToAction("TestTypes");
            }


            testType.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Test Type deleted successfully.";

            return RedirectToAction("TestTypes");
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

            if(existingsampletype != null)
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

            TempData["Success"] = "Sample type updated successfully.";

            return RedirectToAction("SampleTypes");
        }


       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSampletype(int id)
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

            TempData["Success"] =$"{sample.SampleName} deleted successfully.";

            return RedirectToAction("SampleTypes");
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
                    .Where(x=> x.IsActive)
                    .OrderBy(x => x.SupplierName)
                    .ToListAsync();

                return View(consumable);
            }
            var existinGCONSUMABLE = await _context.Consumables.FirstOrDefaultAsync(x => x.ConsumableName == consumable.ConsumableName);

            if(existinGCONSUMABLE != null)
            {
                ModelState.AddModelError("ConsumableName", "The consumable already exist.");
                return View(consumable);
            }
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(x => x.SupplierId == consumable.SupplierId && x.IsActive);

            if(supplier == null)
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
        [HttpGet]
        public IActionResult AdjustStock(int id)
        {
            var consumable = _context.Consumables
                .FirstOrDefault(c => c.ConsumableId == id);

            if (consumable == null)
            {
                return NotFound();
            }

            var vm = new StockAdjustmentVM
            {
                ConsumableID = consumable.ConsumableId,
                ConsumableName = consumable.ConsumableName,
                QuantityOnHand = consumable.QuantityInHand
            };

            return View(vm);
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

            if(existingSupplier != null)
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

            // =====================================================
            // CHECK EMAIL
            // ONLY ACTIVE SUPPLIERS
            // EXCLUDE CURRENT SUPPLIER
            // =====================================================

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
                $"{supplier.SupplierName} has been deactivated successfully.";

            return RedirectToAction("Suppliers");
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
            else if (items.All(i => i.Status == OrderItemStatus.Rejected))
            {
                order.Status = OrderStatus.Rejected;
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

            item.Status = OrderItemStatus.Rejected;
            item.DateRejected = DateTime.Now;
            item.RejectReason = reason;

            var items = order.OrderItems.ToList();

            if (items.All(i => i.Status == OrderItemStatus.Received))
            {
                order.Status = OrderStatus.Completed;
                order.DateCompleted = DateTime.Now;
            }
            else if (items.All(i => i.Status == OrderItemStatus.Rejected))
            {
                order.Status = OrderStatus.Rejected;
                order.DateCancelled = DateTime.Now;
            }
            else if (items.Any(i => i.Status == OrderItemStatus.Received) &&
                     items.Any(i => i.Status == OrderItemStatus.Rejected))
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

        #endregion

        #region Doctor

        public async Task<IActionResult> Doctors()
        {
            var doctor = await _context.Doctors
                .Include(x => x.User)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Surname)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return View(doctor);
        }
        // ==========================================
        // ADD DOCTOR - GET
        // ==========================================

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

        // =========================================================
        // EDIT DOCTOR - GET
        // =========================================================
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




        // =========================================================
        // EDIT DOCTOR - POST
        // =========================================================

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

            // ============================
            // UPDATE DOCTOR
            // ============================

            doctor.Name = model.Name;
            doctor.Surname = model.Surname;
            doctor.PhoneNumber = model.PhoneNumber;
            doctor.HPCSANumber = model.HPCSANumber;
            doctor.IsActive = model.IsActive;

            // ============================
            // UPDATE IDENTITY USER
            // ============================

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

           
            technician.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Technician {technician.Name} {technician.Surname} has been deactivated successfully.";

            return RedirectToAction(nameof(LabTechnician));
        }


        #endregion
    }
}

