using HemaCore.Data;
using HemaCore.Models;
using HemaCore.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;

        public AdminController(
            AppDbContext context,
            UserManager<Users> userManager,
            SignInManager<Users> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var conditionCount = await _context.Conditions
                .CountAsync(c => c.IsActive);

            var conditionCategoryCount = await _context.ConditionCategories
                .CountAsync(c => c.IsActive);

            var allergyCount = await _context.Allergies
                .CountAsync(a => a.IsActive);

            var allergyCategoryCount = await _context.AllergyCategories
                .CountAsync(c => c.IsActive);

            var medicationCount = await _context.Medications
                .CountAsync(m => m.IsActive);

            var medicationCategoryCount = await _context.MedicationCategories
                .CountAsync(c => c.IsActive);

            var conditionRecords = await _context.PatientConditions.CountAsync();
            var allergyRecords = await _context.PatientAllergies.CountAsync();
            var medicationRecords = await _context.PatientMedications.CountAsync();

            var patientRecordCount =
                conditionRecords +
                allergyRecords +
                medicationRecords;

            ViewBag.ConditionCount = conditionCount;
            ViewBag.ConditionCategoryCount = conditionCategoryCount;

            ViewBag.AllergyCount = allergyCount;
            ViewBag.AllergyCategoryCount = allergyCategoryCount;

            ViewBag.MedicationCount = medicationCount;
            ViewBag.MedicationCategoryCount = medicationCategoryCount;

            ViewBag.PatientRecordCount = patientRecordCount;

            return View();
        }

        #endregion


        #region Dashboard

        [HttpGet]
        public async Task<IActionResult> DashboardStats()
        {
            var conditionCount = await _context.Conditions
                .CountAsync(c => c.IsActive);

            var conditionCategoryCount = await _context.ConditionCategories
                .CountAsync(c => c.IsActive);

            var allergyCount = await _context.Allergies
                .CountAsync(a => a.IsActive);

            var allergyCategoryCount = await _context.AllergyCategories
                .CountAsync(c => c.IsActive);

            var medicationCount = await _context.Medications
                .CountAsync(m => m.IsActive);

            var medicationCategoryCount = await _context.MedicationCategories
                .CountAsync(c => c.IsActive);

            var conditionRecords = await _context.PatientConditions.CountAsync();
            var allergyRecords = await _context.PatientAllergies.CountAsync();
            var medicationRecords = await _context.PatientMedications.CountAsync();

            var patientRecordCount =
                conditionRecords +
                allergyRecords +
                medicationRecords;

            return Json(new
            {
                conditionCount,
                conditionCategoryCount,
                allergyCount,
                allergyCategoryCount,
                medicationCount,
                medicationCategoryCount,
                patientRecordCount
            });
        }

        #endregion


        // ============================================================
        // CONDITIONS
        // ============================================================

        #region Conditions

        public async Task<IActionResult> Conditions()
        {
            var conditions = await _context.Conditions
                .Include(c => c.Category)
                .OrderBy(c => c.Category.Name)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var conditionCountByCategory = await _context.Conditions
                .GroupBy(c => c.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(
                    x => x.CategoryId,
                    x => x.Count);

            var categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.ConditionCount = conditions.Count;
            ViewBag.ConditionCountByCategory = conditionCountByCategory;
            ViewBag.Categories = categories;

            return View(conditions);
        }


        public async Task<IActionResult> AddCondition()
        {
            var categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            if (!categories.Any())
            {
                TempData["Error"] =
                    "Please create a condition category first before adding conditions.";

                return RedirectToAction(nameof(ConditionCategories));
            }

            ViewBag.Categories = categories;

            return View(new Condition());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCondition(Condition condition)
        {
            ModelState.Remove("Category");
            ModelState.Remove("PatientConditions");

            if (ModelState.IsValid)
            {
                try
                {
                    var exists = await _context.Conditions
                        .AnyAsync(c =>
                            c.Name.ToLower() ==
                            condition.Name.Trim().ToLower() &&
                            c.CategoryId == condition.CategoryId &&
                            c.IsActive);

                    if (exists)
                    {
                        TempData["Error"] =
                            "A condition with this name already exists in the selected category.";

                        ViewBag.Categories =
                            await _context.ConditionCategories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .ToListAsync();

                        return View(condition);
                    }

                    condition.IsActive = true;
                    condition.Name = condition.Name.Trim();

                    if (condition.Description != null)
                    {
                        condition.Description =
                            condition.Description.Trim();
                    }

                    _context.Conditions.Add(condition);

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Condition added successfully!";

                    return RedirectToAction(nameof(Conditions));
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while saving: " +
                        ex.Message;

                    ViewBag.Categories =
                        await _context.ConditionCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();

                    return View(condition);
                }
            }

            ViewBag.Categories =
                await _context.ConditionCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(condition);
        }


        public async Task<IActionResult> EditCondition(int id)
        {
            var condition = await _context.Conditions
                .Include(c => c.Category)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (condition == null)
            {
                TempData["Error"] =
                    "Condition not found.";

                return RedirectToAction(nameof(Conditions));
            }

            ViewBag.Categories =
                await _context.ConditionCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(condition);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCondition(
            int id,
            Condition condition)
        {
            if (id != condition.Id)
            {
                TempData["Error"] =
                    "Invalid condition ID.";

                return RedirectToAction(nameof(Conditions));
            }

            ModelState.Remove("Category");
            ModelState.Remove("PatientConditions");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing =
                        await _context.Conditions.FindAsync(id);

                    if (existing == null)
                    {
                        TempData["Error"] =
                            "Condition not found.";

                        return RedirectToAction(nameof(Conditions));
                    }

                    var duplicate =
                        await _context.Conditions
                            .AnyAsync(c =>
                                c.Name.ToLower() ==
                                condition.Name.Trim().ToLower() &&
                                c.CategoryId ==
                                condition.CategoryId &&
                                c.Id != id &&
                                c.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] =
                            "A condition with this name already exists in the selected category.";

                        ViewBag.Categories =
                            await _context.ConditionCategories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .ToListAsync();

                        return View(condition);
                    }

                    existing.Name =
                        condition.Name.Trim();

                    existing.Description =
                        condition.Description?.Trim();

                    existing.CategoryId =
                        condition.CategoryId;

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Condition updated successfully!";

                    return RedirectToAction(nameof(Conditions));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await ConditionExists(condition.Id))
                    {
                        TempData["Error"] =
                            "Condition not found.";

                        return RedirectToAction(nameof(Conditions));
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while updating: " +
                        ex.Message;
                }
            }

            ViewBag.Categories =
                await _context.ConditionCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(condition);
        }


        public async Task<IActionResult> DeleteCondition(int id)
        {
            var condition =
                await _context.Conditions
                    .Include(c => c.Category)
                    .FirstOrDefaultAsync(c => c.Id == id);

            if (condition == null)
            {
                TempData["Error"] =
                    "Condition not found.";

                return RedirectToAction(nameof(Conditions));
            }

            var isUsed =
                await _context.PatientConditions
                    .AnyAsync(pc => pc.ConditionId == id);

            if (isUsed)
            {
                TempData["Error"] =
                    "This condition cannot be deleted as it is being used by patients. You can deactivate it instead.";

                return RedirectToAction(nameof(Conditions));
            }

            return View(condition);
        }


        [HttpPost, ActionName("DeleteCondition")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteConditionConfirmed(int id)
        {
            var condition =
                await _context.Conditions.FindAsync(id);

            if (condition != null)
            {
                condition.IsActive = false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Condition deactivated successfully!";
            }

            return RedirectToAction(nameof(Conditions));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ToggleConditionStatus(int id)
        {
            var condition =
                await _context.Conditions
                    .FirstOrDefaultAsync(c => c.Id == id);

            if (condition == null)
            {
                TempData["Error"] =
                    "Condition not found.";

                return RedirectToAction(nameof(Conditions));
            }

            condition.IsActive =
                !condition.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                condition.IsActive
                    ? "Condition activated successfully."
                    : "Condition deactivated successfully.";

            return RedirectToAction(nameof(Conditions));
        }


        private async Task<bool>
            ConditionExists(int id)
        {
            return await _context.Conditions
                .AnyAsync(e => e.Id == id);
        }


        public async Task<IActionResult>
            SearchConditionsLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allConditions =
                    await _context.Conditions
                        .Include(c => c.Category)
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Category.Name)
                        .ThenBy(c => c.Name)
                        .Select(c => new
                        {
                            id = c.Id,
                            name = c.Name,
                            categoryName =
                                c.Category.Name,
                            description =
                                c.Description,
                            isActive =
                                c.IsActive
                        })
                        .ToListAsync();

                return Json(allConditions);
            }

            var conditions =
                await _context.Conditions
                    .Include(c => c.Category)
                    .Where(c =>
                        c.IsActive &&
                        (
                            c.Name.Contains(term) ||
                            (
                                c.Description != null &&
                                c.Description.Contains(term)
                            ) ||
                            c.Category.Name.Contains(term)
                        ))
                    .OrderBy(c => c.Category.Name)
                    .ThenBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        categoryName =
                            c.Category.Name,
                        description =
                            c.Description,
                        isActive =
                            c.IsActive
                    })
                    .ToListAsync();

            return Json(conditions);
        }


        // ============================================================
        // CONDITION CATEGORIES
        // ============================================================

        public async Task<IActionResult>
            ConditionCategories()
        {
            var categories =
                await _context.ConditionCategories
                    .Include(c => c.Conditions)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            var conditionCountByCategory =
                await _context.Conditions
                    .Where(c => c.IsActive)
                    .GroupBy(c => c.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.CategoryId,
                        x => x.Count);

            ViewBag.CategoryCount =
                categories.Count;

            ViewBag.ConditionCountByCategory =
                conditionCountByCategory;

            return View(categories);
        }


        [HttpGet]
        public IActionResult AddConditionCategory()
        {
            return View(new ConditionCategory());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            AddConditionCategory(
                ConditionCategory category)
        {
            ModelState.Remove("Conditions");

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError(
                    "Name",
                    "Category name is required.");
            }

            if (string.IsNullOrWhiteSpace(category.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Category description is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name =
                category.Name.Trim();

            category.Description =
                category.Description.Trim();

            var exists =
                await _context.ConditionCategories
                    .AnyAsync(c =>
                        c.Name.ToLower() ==
                        category.Name.ToLower() &&
                        c.IsActive);

            if (exists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A category with this name already exists.");

                return View(category);
            }

            category.IsActive = true;

            _context.ConditionCategories.Add(category);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Condition category added successfully!";

            // STAYS ON CONDITION CATEGORY PAGE
            return RedirectToAction(
                nameof(ConditionCategories));
        }


        [HttpGet]
        public async Task<IActionResult>
            EditConditionCategory(int id)
        {
            var category =
                await _context.ConditionCategories
                    .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(ConditionCategories));
            }

            return View(category);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            EditConditionCategory(
                int id,
                ConditionCategory category)
        {
            if (id != category.Id)
            {
                TempData["Error"] =
                    "Invalid category ID.";

                return RedirectToAction(
                    nameof(ConditionCategories));
            }

            ModelState.Remove("Conditions");

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError(
                    "Name",
                    "Category name is required.");
            }

            if (string.IsNullOrWhiteSpace(category.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Category description is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name =
                category.Name.Trim();

            category.Description =
                category.Description.Trim();

            var duplicate =
                await _context.ConditionCategories
                    .AnyAsync(c =>
                        c.Name.ToLower() ==
                        category.Name.ToLower() &&
                        c.Id != id &&
                        c.IsActive);

            if (duplicate)
            {
                ModelState.AddModelError(
                    "Name",
                    "A category with this name already exists.");

                return View(category);
            }

            var existing =
                await _context.ConditionCategories
                    .FindAsync(id);

            if (existing == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(ConditionCategories));
            }

            existing.Name =
                category.Name;

            existing.Description =
                category.Description;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Condition category updated successfully!";

            // STAYS ON CONDITION CATEGORY PAGE
            return RedirectToAction(
                nameof(ConditionCategories));
        }


        public async Task<IActionResult>
            DeleteConditionCategory(int id)
        {
            var category =
                await _context.ConditionCategories
                    .Include(c => c.Conditions)
                    .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(ConditionCategories));
            }

            var hasActiveConditions =
                await _context.Conditions
                    .AnyAsync(c =>
                        c.CategoryId == id &&
                        c.IsActive);

            if (hasActiveConditions)
            {
                TempData["Error"] =
                    "This category cannot be deleted as it has active conditions. Please deactivate or move the conditions first.";

                return RedirectToAction(
                    nameof(ConditionCategories));
            }

            return View(category);
        }


        [HttpPost, ActionName("DeleteConditionCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteConditionCategoryConfirmed(int id)
        {
            var category =
                await _context.ConditionCategories
                    .FindAsync(id);

            if (category != null)
            {
                category.IsActive = false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Category deactivated successfully!";
            }

            return RedirectToAction(
                nameof(ConditionCategories));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ToggleCategoryStatus(int id)
        {
            var category =
                await _context.ConditionCategories
                    .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Condition category not found.";

                return RedirectToAction(
                    nameof(ConditionCategories));
            }

            if (category.IsActive)
            {
                var hasActiveConditions =
                    await _context.Conditions
                        .AnyAsync(c =>
                            c.CategoryId == id &&
                            c.IsActive);

                if (hasActiveConditions)
                {
                    TempData["Error"] =
                        "Cannot deactivate this category because it still contains active conditions. Please deactivate or move the conditions first.";

                    return RedirectToAction(
                        nameof(ConditionCategories));
                }
            }

            category.IsActive =
                !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                category.IsActive
                    ? "Condition category activated successfully!"
                    : "Condition category deactivated successfully!";

            return RedirectToAction(
                nameof(ConditionCategories));
        }


        public async Task<IActionResult>
            SearchCategoriesLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allCategories =
                    await _context.ConditionCategories
                        .OrderBy(c => c.Name)
                        .Select(c => new
                        {
                            id = c.Id,
                            name = c.Name,
                            description =
                                c.Description,
                            conditionCount =
                                c.Conditions.Count(
                                    cd => cd.IsActive),
                            isActive =
                                c.IsActive
                        })
                        .ToListAsync();

                return Json(allCategories);
            }

            var categories =
                await _context.ConditionCategories
                    .Where(c =>
                        c.Name.Contains(term) ||
                        c.Description.Contains(term))
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        description =
                            c.Description,
                        conditionCount =
                            c.Conditions.Count(
                                cd => cd.IsActive),
                        isActive =
                            c.IsActive
                    })
                    .ToListAsync();

            return Json(categories);
        }


        private async Task<bool>
            CategoryExists(int id)
        {
            return await _context.ConditionCategories
                .AnyAsync(e => e.Id == id);
        }

        #endregion


        // ============================================================
        // ALLERGIES
        // ============================================================

        #region Allergies

        public async Task<IActionResult> Allergies()
        {
            var allergies =
                await _context.Allergies
                    .Include(a => a.Category)
                    .OrderBy(a => a.Category.Name)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

            var allergyCountByCategory =
                await _context.Allergies
                    .GroupBy(a => a.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.CategoryId,
                        x => x.Count);

            var categories =
                await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            ViewBag.AllergyCount =
                allergies.Count;

            ViewBag.AllergyCountByCategory =
                allergyCountByCategory;

            ViewBag.Categories =
                categories;

            return View(allergies);
        }


        public async Task<IActionResult> AddAllergy()
        {
            var categories =
                await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            if (!categories.Any())
            {
                TempData["Error"] =
                    "Please create an allergy category first before adding allergies.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            ViewBag.Categories =
                categories;

            return View(new Allergy());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            AddAllergy(Allergy allergy)
        {
            ModelState.Remove("Category");
            ModelState.Remove("PatientAllergies");

            if (ModelState.IsValid)
            {
                try
                {
                    var exists =
                        await _context.Allergies
                            .AnyAsync(a =>
                                a.Name.ToLower() ==
                                allergy.Name.Trim()
                                    .ToLower() &&
                                a.CategoryId ==
                                allergy.CategoryId &&
                                a.IsActive);

                    if (exists)
                    {
                        TempData["Error"] =
                            "An allergy with this name already exists in the selected category.";

                        ViewBag.Categories =
                            await _context.AllergyCategories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .ToListAsync();

                        return View(allergy);
                    }

                    allergy.IsActive = true;

                    allergy.Name =
                        allergy.Name.Trim();

                    allergy.Description =
                        allergy.Description.Trim();

                    _context.Allergies.Add(allergy);

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Allergy added successfully!";

                    return RedirectToAction(
                        nameof(Allergies));
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while saving: " +
                        ex.Message;

                    ViewBag.Categories =
                        await _context.AllergyCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();

                    return View(allergy);
                }
            }

            ViewBag.Categories =
                await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(allergy);
        }


        public async Task<IActionResult>
            EditAllergy(int id)
        {
            var allergy =
                await _context.Allergies
                    .Include(a => a.Category)
                    .FirstOrDefaultAsync(
                        a => a.Id == id);

            if (allergy == null)
            {
                TempData["Error"] =
                    "Allergy not found.";

                return RedirectToAction(
                    nameof(Allergies));
            }

            ViewBag.Categories =
                await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(allergy);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            EditAllergy(
                int id,
                Allergy allergy)
        {
            if (id != allergy.Id)
            {
                TempData["Error"] =
                    "Invalid allergy ID.";

                return RedirectToAction(
                    nameof(Allergies));
            }

            ModelState.Remove("Category");
            ModelState.Remove("PatientAllergies");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing =
                        await _context.Allergies
                            .FirstOrDefaultAsync(
                                a => a.Id == id);

                    if (existing == null)
                    {
                        TempData["Error"] =
                            "Allergy not found.";

                        return RedirectToAction(
                            nameof(Allergies));
                    }

                    var duplicate =
                        await _context.Allergies
                            .AnyAsync(a =>
                                a.Name.ToLower() ==
                                allergy.Name.Trim()
                                    .ToLower() &&
                                a.CategoryId ==
                                allergy.CategoryId &&
                                a.Id != id &&
                                a.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] =
                            "An allergy with this name already exists in the selected category.";

                        ViewBag.Categories =
                            await _context.AllergyCategories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .ToListAsync();

                        return View(allergy);
                    }

                    existing.Name =
                        allergy.Name.Trim();

                    existing.Description =
                        allergy.Description.Trim();

                    existing.CategoryId =
                        allergy.CategoryId;

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Allergy updated successfully!";

                    return RedirectToAction(
                        nameof(Allergies));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await AllergyExists(
                            allergy.Id))
                    {
                        TempData["Error"] =
                            "Allergy not found.";

                        return RedirectToAction(
                            nameof(Allergies));
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while updating: " +
                        ex.Message;
                }
            }

            ViewBag.Categories =
                await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(allergy);
        }


        public async Task<IActionResult>
            DeleteAllergy(int id)
        {
            var allergy =
                await _context.Allergies
                    .Include(a => a.Category)
                    .FirstOrDefaultAsync(
                        a => a.Id == id);

            if (allergy == null)
            {
                TempData["Error"] =
                    "Allergy not found.";

                return RedirectToAction(
                    nameof(Allergies));
            }

            var isUsed =
                await _context.PatientAllergies
                    .AnyAsync(pa =>
                        pa.AllergyId == id);

            if (isUsed)
            {
                TempData["Error"] =
                    "This allergy cannot be deleted as it is being used by patients. You can deactivate it instead.";

                return RedirectToAction(
                    nameof(Allergies));
            }

            return View(allergy);
        }


        [HttpPost, ActionName("DeleteAllergy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteAllergyConfirmed(int id)
        {
            var allergy =
                await _context.Allergies
                    .FindAsync(id);

            if (allergy != null)
            {
                allergy.IsActive = false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Allergy deactivated successfully!";
            }

            return RedirectToAction(
                nameof(Allergies));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ToggleAllergyStatus(int id)
        {
            var allergy =
                await _context.Allergies
                    .FirstOrDefaultAsync(
                        a => a.Id == id);

            if (allergy == null)
            {
                TempData["Error"] =
                    "Allergy not found.";

                return RedirectToAction(
                    nameof(Allergies));
            }

            allergy.IsActive =
                !allergy.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                allergy.IsActive
                    ? "Allergy activated successfully."
                    : "Allergy deactivated successfully.";

            return RedirectToAction(
                nameof(Allergies));
        }


        private async Task<bool>
            AllergyExists(int id)
        {
            return await _context.Allergies
                .AnyAsync(e => e.Id == id);
        }


        public async Task<IActionResult>
            SearchAllergiesLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allAllergies =
                    await _context.Allergies
                        .Include(a => a.Category)
                        .Where(a => a.IsActive)
                        .OrderBy(a => a.Category.Name)
                        .ThenBy(a => a.Name)
                        .Select(a => new
                        {
                            id = a.Id,
                            name = a.Name,
                            categoryName =
                                a.Category.Name,
                            description =
                                a.Description,
                            isActive =
                                a.IsActive
                        })
                        .ToListAsync();

                return Json(allAllergies);
            }

            var allergies =
                await _context.Allergies
                    .Include(a => a.Category)
                    .Where(a =>
                        a.IsActive &&
                        (
                            a.Name.Contains(term) ||
                            a.Category.Name.Contains(term)
                        ))
                    .OrderBy(a => a.Category.Name)
                    .ThenBy(a => a.Name)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        categoryName =
                            a.Category.Name,
                        description =
                            a.Description,
                        isActive =
                            a.IsActive
                    })
                    .ToListAsync();

            return Json(allergies);
        }


        // ============================================================
        // ALLERGY CATEGORIES
        // ============================================================

        public async Task<IActionResult>
            AllergyCategories()
        {
            var categories =
                await _context.AllergyCategories
                    .Include(c => c.Allergies)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            var allergyCountByCategory =
                await _context.Allergies
                    .Where(a => a.IsActive)
                    .GroupBy(a => a.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.CategoryId,
                        x => x.Count);

            ViewBag.CategoryCount =
                categories.Count;

            ViewBag.AllergyCountByCategory =
                allergyCountByCategory;

            return View(categories);
        }


        [HttpGet]
        public IActionResult AddAllergyCategory()
        {
            return View(new AllergyCategory());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            AddAllergyCategory(
                AllergyCategory category)
        {
            ModelState.Remove("Allergies");

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError(
                    "Name",
                    "Category name is required.");
            }

            if (string.IsNullOrWhiteSpace(category.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Category description is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name =
                category.Name.Trim();

            category.Description =
                category.Description.Trim();

            var exists =
                await _context.AllergyCategories
                    .AnyAsync(c =>
                        c.Name.ToLower() ==
                        category.Name.ToLower() &&
                        c.IsActive);

            if (exists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A category with this name already exists.");

                return View(category);
            }

            category.IsActive = true;

            _context.AllergyCategories.Add(category);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Allergy category added successfully!";

            // STAYS ON ALLERGY CATEGORY PAGE
            return RedirectToAction(
                nameof(AllergyCategories));
        }


        [HttpGet]
        public async Task<IActionResult>
            EditAllergyCategory(int id)
        {
            var category =
                await _context.AllergyCategories
                    .FirstOrDefaultAsync(
                        c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            return View(category);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            EditAllergyCategory(
                int id,
                AllergyCategory category)
        {
            if (id != category.Id)
            {
                TempData["Error"] =
                    "Invalid category ID.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            ModelState.Remove("Allergies");

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError(
                    "Name",
                    "Category name is required.");
            }

            if (string.IsNullOrWhiteSpace(category.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Category description is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name =
                category.Name.Trim();

            category.Description =
                category.Description.Trim();

            var duplicate =
                await _context.AllergyCategories
                    .AnyAsync(c =>
                        c.Name.ToLower() ==
                        category.Name.ToLower() &&
                        c.Id != id &&
                        c.IsActive);

            if (duplicate)
            {
                ModelState.AddModelError(
                    "Name",
                    "A category with this name already exists.");

                return View(category);
            }

            var existing =
                await _context.AllergyCategories
                    .FindAsync(id);

            if (existing == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            existing.Name =
                category.Name;

            existing.Description =
                category.Description;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Allergy category updated successfully!";

            // STAYS ON ALLERGY CATEGORY PAGE
            return RedirectToAction(
                nameof(AllergyCategories));
        }


        public async Task<IActionResult>
            DeleteAllergyCategory(int id)
        {
            var category =
                await _context.AllergyCategories
                    .Include(c => c.Allergies)
                    .FirstOrDefaultAsync(
                        c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            var hasActiveAllergies =
                await _context.Allergies
                    .AnyAsync(a =>
                        a.CategoryId == id &&
                        a.IsActive);

            if (hasActiveAllergies)
            {
                TempData["Error"] =
                    "This category cannot be deleted as it has active allergies. Please deactivate or move the allergies first.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            return View(category);
        }


        [HttpPost, ActionName("DeleteAllergyCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteAllergyCategoryConfirmed(int id)
        {
            var category =
                await _context.AllergyCategories
                    .FindAsync(id);

            if (category != null)
            {
                category.IsActive = false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Category deactivated successfully!";
            }

            return RedirectToAction(
                nameof(AllergyCategories));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ToggleAllergyCategoryStatus(int id)
        {
            var category =
                await _context.AllergyCategories
                    .FirstOrDefaultAsync(
                        c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Allergy category not found.";

                return RedirectToAction(
                    nameof(AllergyCategories));
            }

            if (category.IsActive)
            {
                var hasActiveAllergies =
                    await _context.Allergies
                        .AnyAsync(a =>
                            a.CategoryId == id &&
                            a.IsActive);

                if (hasActiveAllergies)
                {
                    TempData["Error"] =
                        "Cannot deactivate this category because it still contains active allergies. Please deactivate or move the allergies first.";

                    return RedirectToAction(
                        nameof(AllergyCategories));
                }
            }

            category.IsActive =
                !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                category.IsActive
                    ? "Allergy category activated successfully!"
                    : "Allergy category deactivated successfully!";

            return RedirectToAction(
                nameof(AllergyCategories));
        }


        public async Task<IActionResult>
            SearchAllergyCategoriesLive(string term)
        {
            var query =
                _context.AllergyCategories
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                query =
                    query.Where(c =>
                        c.Name.Contains(term) ||
                        c.Description.Contains(term));
            }

            var categories =
                await query
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        description =
                            c.Description,
                        allergyCount =
                            c.Allergies.Count(
                                a => a.IsActive),
                        isActive =
                            c.IsActive
                    })
                    .ToListAsync();

            return Json(categories);
        }


        private async Task<bool>
            AllergyCategoryExists(int id)
        {
            return await _context.AllergyCategories
                .AnyAsync(e => e.Id == id);
        }

        #endregion


        // ============================================================
        // MEDICATIONS
        // ============================================================

        #region Medications

        public async Task<IActionResult> Medications()
        {
            var medications =
                await _context.Medications
                    .Include(m => m.Category)
                    .OrderBy(m => m.Category.Name)
                    .ThenBy(m => m.Name)
                    .ToListAsync();

            var medicationCountByCategory =
                await _context.Medications
                    .GroupBy(m => m.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.CategoryId,
                        x => x.Count);

            var categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            ViewBag.MedicationCount =
                medications.Count;

            ViewBag.MedicationCountByCategory =
                medicationCountByCategory;

            ViewBag.Categories =
                categories;

            return View(medications);
        }


        public async Task<IActionResult> AddMedication()
        {
            var categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            if (!categories.Any())
            {
                TempData["Error"] =
                    "Please create a medication category first before adding medications.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            ViewBag.Categories =
                categories;

            return View(new Medication());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            AddMedication(Medication medication)
        {
            ModelState.Remove("Category");
            ModelState.Remove("PatientMedications");

            if (ModelState.IsValid)
            {
                try
                {
                    var exists =
                        await _context.Medications
                            .AnyAsync(m =>
                                m.Name.ToLower() ==
                                medication.Name.Trim()
                                    .ToLower() &&
                                m.CategoryId ==
                                medication.CategoryId &&
                                m.IsActive);

                    if (exists)
                    {
                        TempData["Error"] =
                            "A medication with this name already exists in the selected category.";

                        ViewBag.Categories =
                            await _context.MedicationCategories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .ToListAsync();

                        return View(medication);
                    }

                    medication.IsActive = true;

                    medication.Name =
                        medication.Name.Trim();

                    medication.Description =
                        medication.Description.Trim();

                    _context.Medications.Add(
                        medication);

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Medication added successfully!";

                    return RedirectToAction(
                        nameof(Medications));
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while saving: " +
                        ex.Message;

                    ViewBag.Categories =
                        await _context.MedicationCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();

                    return View(medication);
                }
            }

            ViewBag.Categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(medication);
        }


        public async Task<IActionResult>
            EditMedication(int id)
        {
            var medication =
                await _context.Medications
                    .Include(m => m.Category)
                    .FirstOrDefaultAsync(
                        m => m.Id == id);

            if (medication == null)
            {
                TempData["Error"] =
                    "Medication not found.";

                return RedirectToAction(
                    nameof(Medications));
            }

            ViewBag.Categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(medication);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            EditMedication(
                int id,
                Medication medication)
        {
            if (id != medication.Id)
            {
                TempData["Error"] =
                    "Invalid medication ID.";

                return RedirectToAction(
                    nameof(Medications));
            }

            ModelState.Remove("Category");
            ModelState.Remove("PatientMedications");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing =
                        await _context.Medications
                            .FirstOrDefaultAsync(
                                m => m.Id == id);

                    if (existing == null)
                    {
                        TempData["Error"] =
                            "Medication not found.";

                        return RedirectToAction(
                            nameof(Medications));
                    }

                    var duplicate =
                        await _context.Medications
                            .AnyAsync(m =>
                                m.Name.ToLower() ==
                                medication.Name.Trim()
                                    .ToLower() &&
                                m.CategoryId ==
                                medication.CategoryId &&
                                m.Id != id &&
                                m.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] =
                            "A medication with this name already exists in the selected category.";

                        ViewBag.Categories =
                            await _context.MedicationCategories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .ToListAsync();

                        return View(medication);
                    }

                    existing.Name =
                        medication.Name.Trim();

                    existing.Description =
                        medication.Description.Trim();

                    existing.CategoryId =
                        medication.CategoryId;

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Medication updated successfully!";

                    return RedirectToAction(
                        nameof(Medications));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await MedicationExists(
                            medication.Id))
                    {
                        TempData["Error"] =
                            "Medication not found.";

                        return RedirectToAction(
                            nameof(Medications));
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while updating: " +
                        ex.Message;
                }
            }

            ViewBag.Categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(medication);
        }


        public async Task<IActionResult>
            DeleteMedication(int id)
        {
            var medication =
                await _context.Medications
                    .Include(m => m.Category)
                    .FirstOrDefaultAsync(
                        m => m.Id == id);

            if (medication == null)
            {
                TempData["Error"] =
                    "Medication not found.";

                return RedirectToAction(
                    nameof(Medications));
            }

            var isUsed =
                await _context.PatientMedications
                    .AnyAsync(pm =>
                        pm.MedicationId == id);

            if (isUsed)
            {
                TempData["Error"] =
                    "This medication cannot be deleted as it is being used by patients. You can deactivate it instead.";

                return RedirectToAction(
                    nameof(Medications));
            }

            return View(medication);
        }


        [HttpPost, ActionName("DeleteMedication")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteMedicationConfirmed(int id)
        {
            var medication =
                await _context.Medications
                    .FindAsync(id);

            if (medication != null)
            {
                medication.IsActive = false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Medication deactivated successfully!";
            }

            return RedirectToAction(
                nameof(Medications));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ToggleMedicationStatus(int id)
        {
            var medication =
                await _context.Medications
                    .FirstOrDefaultAsync(
                        m => m.Id == id);

            if (medication == null)
            {
                TempData["Error"] =
                    "Medication not found.";

                return RedirectToAction(
                    nameof(Medications));
            }

            medication.IsActive =
                !medication.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                medication.IsActive
                    ? "Medication activated successfully."
                    : "Medication deactivated successfully.";

            return RedirectToAction(
                nameof(Medications));
        }


        private async Task<bool>
            MedicationExists(int id)
        {
            return await _context.Medications
                .AnyAsync(e => e.Id == id);
        }


        public async Task<IActionResult>
            SearchMedicationsLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allMedications =
                    await _context.Medications
                        .Include(m => m.Category)
                        .Where(m => m.IsActive)
                        .OrderBy(m => m.Category.Name)
                        .ThenBy(m => m.Name)
                        .Select(m => new
                        {
                            id = m.Id,
                            name = m.Name,
                            categoryName =
                                m.Category.Name,
                            description =
                                m.Description,
                            isActive =
                                m.IsActive
                        })
                        .ToListAsync();

                return Json(allMedications);
            }

            var medications =
                await _context.Medications
                    .Include(m => m.Category)
                    .Where(m =>
                        m.IsActive &&
                        (
                            m.Name.Contains(term) ||
                            m.Category.Name.Contains(term)
                        ))
                    .OrderBy(m => m.Category.Name)
                    .ThenBy(m => m.Name)
                    .Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        categoryName =
                            m.Category.Name,
                        description =
                            m.Description,
                        isActive =
                            m.IsActive
                    })
                    .ToListAsync();

            return Json(medications);
        }


        // ============================================================
        // MEDICATION CATEGORIES
        // ============================================================

        public async Task<IActionResult>
            MedicationCategories()
        {
            var categories =
                await _context.MedicationCategories
                    .Include(c => c.Medications)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            var medicationCountByCategory =
                await _context.Medications
                    .Where(m => m.IsActive)
                    .GroupBy(m => m.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.CategoryId,
                        x => x.Count);

            ViewBag.CategoryCount =
                categories.Count;

            ViewBag.MedicationCountByCategory =
                medicationCountByCategory;

            return View(categories);
        }


        [HttpGet]
        public IActionResult AddMedicationCategory()
        {
            return View(new MedicationCategory());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            AddMedicationCategory(
                MedicationCategory category)
        {
            ModelState.Remove("Medications");

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError(
                    "Name",
                    "Category name is required.");
            }

            if (string.IsNullOrWhiteSpace(category.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Category description is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name =
                category.Name.Trim();

            category.Description =
                category.Description.Trim();

            var exists =
                await _context.MedicationCategories
                    .AnyAsync(c =>
                        c.Name.ToLower() ==
                        category.Name.ToLower() &&
                        c.IsActive);

            if (exists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A category with this name already exists.");

                return View(category);
            }

            category.IsActive = true;

            _context.MedicationCategories.Add(
                category);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Medication category added successfully!";

            // STAYS ON MEDICATION CATEGORY PAGE
            return RedirectToAction(
                nameof(MedicationCategories));
        }


        [HttpGet]
        public async Task<IActionResult>
            EditMedicationCategory(int id)
        {
            var category =
                await _context.MedicationCategories
                    .FirstOrDefaultAsync(
                        c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            return View(category);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            EditMedicationCategory(
                int id,
                MedicationCategory category)
        {
            if (id != category.Id)
            {
                TempData["Error"] =
                    "Invalid category ID.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            ModelState.Remove("Medications");

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError(
                    "Name",
                    "Category name is required.");
            }

            if (string.IsNullOrWhiteSpace(category.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Category description is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name =
                category.Name.Trim();

            category.Description =
                category.Description.Trim();

            var duplicate =
                await _context.MedicationCategories
                    .AnyAsync(c =>
                        c.Name.ToLower() ==
                        category.Name.ToLower() &&
                        c.Id != id &&
                        c.IsActive);

            if (duplicate)
            {
                ModelState.AddModelError(
                    "Name",
                    "A category with this name already exists.");

                return View(category);
            }

            var existing =
                await _context.MedicationCategories
                    .FindAsync(id);

            if (existing == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            existing.Name =
                category.Name;

            existing.Description =
                category.Description;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Medication category updated successfully!";

            // STAYS ON MEDICATION CATEGORY PAGE
            return RedirectToAction(
                nameof(MedicationCategories));
        }


        public async Task<IActionResult>
            DeleteMedicationCategory(int id)
        {
            var category =
                await _context.MedicationCategories
                    .Include(c => c.Medications)
                    .FirstOrDefaultAsync(
                        c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Category not found.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            var hasActiveMedications =
                await _context.Medications
                    .AnyAsync(m =>
                        m.CategoryId == id &&
                        m.IsActive);

            if (hasActiveMedications)
            {
                TempData["Error"] =
                    "This category cannot be deleted as it has active medications. Please deactivate or move the medications first.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            return View(category);
        }


        [HttpPost, ActionName("DeleteMedicationCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteMedicationCategoryConfirmed(int id)
        {
            var category =
                await _context.MedicationCategories
                    .FindAsync(id);

            if (category != null)
            {
                category.IsActive = false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Category deactivated successfully!";
            }

            return RedirectToAction(
                nameof(MedicationCategories));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ToggleMedicationCategoryStatus(int id)
        {
            var category =
                await _context.MedicationCategories
                    .FirstOrDefaultAsync(
                        c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] =
                    "Medication category not found.";

                return RedirectToAction(
                    nameof(MedicationCategories));
            }

            if (category.IsActive)
            {
                var hasActiveMedications =
                    await _context.Medications
                        .AnyAsync(m =>
                            m.CategoryId == id &&
                            m.IsActive);

                if (hasActiveMedications)
                {
                    TempData["Error"] =
                        "Cannot deactivate this category because it still contains active medications. Please deactivate or move the medications first.";

                    return RedirectToAction(
                        nameof(MedicationCategories));
                }
            }

            category.IsActive =
                !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                category.IsActive
                    ? "Medication category activated successfully!"
                    : "Medication category deactivated successfully!";

            return RedirectToAction(
                nameof(MedicationCategories));
        }


        public async Task<IActionResult>
            SearchMedicationCategoriesLive(string term)
        {
            var query =
                _context.MedicationCategories
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                query =
                    query.Where(c =>
                        c.Name.Contains(term) ||
                        c.Description.Contains(term));
            }

            var categories =
                await query
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        description =
                            c.Description,
                        medicationCount =
                            c.Medications.Count(
                                m => m.IsActive),
                        isActive =
                            c.IsActive
                    })
                    .ToListAsync();

            return Json(categories);
        }


        private async Task<bool>
            MedicationCategoryExists(int id)
        {
            return await _context.MedicationCategories
                .AnyAsync(e => e.Id == id);
        }

        #endregion


        // ============================================================
        // ACCOUNT MANAGEMENT
        // ============================================================

        #region AccountManagement

        public async Task<IActionResult> Profile()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var model =
                new ProfileViewModel
                {
                    Email = user.Email,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber
                };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var existingUser =
                await _userManager.FindByEmailAsync(
                    model.Email);

            if (existingUser != null &&
                existingUser.Id != user.Id)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already taken by another user.");

                return View(model);
            }

            user.Email =
                model.Email;

            user.UserName =
                model.Email;

            user.FullName =
                model.FullName;

            user.PhoneNumber =
                model.PhoneNumber;

            var result =
                await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] =
                    "Profile updated successfully!";

                return RedirectToAction(
                    nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }

            return View(model);
        }


        public IActionResult ChangePassword()
        {
            return View(
                new ChangePasswordViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ChangePassword(
                ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var isPasswordValid =
                await _userManager.CheckPasswordAsync(
                    user,
                    model.CurrentPassword);

            if (!isPasswordValid)
            {
                ModelState.AddModelError(
                    "CurrentPassword",
                    "Current password is incorrect.");

                return View(model);
            }

            var result =
                await _userManager.ChangePasswordAsync(
                    user,
                    model.CurrentPassword,
                    model.NewPassword);

            if (result.Succeeded)
            {
                await _userManager
                    .UpdateSecurityStampAsync(user);

                TempData["Success"] =
                    "Password changed successfully!";

                return RedirectToAction(
                    nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }

            return View(model);
        }

        #endregion

        #region Report
       
// =========================
// REPORTS
// =========================

public async Task<IActionResult> Reports()
        {
            // Conditions
            var totalConditions = await _context.Conditions.CountAsync();
            var activeConditions = await _context.Conditions
                .CountAsync(c => c.IsActive);
            var inactiveConditions = totalConditions - activeConditions;

            // Condition Categories
            var totalConditionCategories = await _context.ConditionCategories.CountAsync();
            var activeConditionCategories = await _context.ConditionCategories
                .CountAsync(c => c.IsActive);
            var inactiveConditionCategories =
                totalConditionCategories - activeConditionCategories;

            // Allergies
            var totalAllergies = await _context.Allergies.CountAsync();
            var activeAllergies = await _context.Allergies
                .CountAsync(a => a.IsActive);
            var inactiveAllergies = totalAllergies - activeAllergies;

            // Allergy Categories
            var totalAllergyCategories = await _context.AllergyCategories.CountAsync();
            var activeAllergyCategories = await _context.AllergyCategories
                .CountAsync(c => c.IsActive);
            var inactiveAllergyCategories =
                totalAllergyCategories - activeAllergyCategories;

            // Medications
            var totalMedications = await _context.Medications.CountAsync();
            var activeMedications = await _context.Medications
                .CountAsync(m => m.IsActive);
            var inactiveMedications = totalMedications - activeMedications;

            // Medication Categories
            var totalMedicationCategories =
                await _context.MedicationCategories.CountAsync();

            var activeMedicationCategories =
                await _context.MedicationCategories
                    .CountAsync(c => c.IsActive);

            var inactiveMedicationCategories =
                totalMedicationCategories - activeMedicationCategories;

            // Send data to the view
            ViewBag.TotalConditions = totalConditions;
            ViewBag.ActiveConditions = activeConditions;
            ViewBag.InactiveConditions = inactiveConditions;

            ViewBag.TotalConditionCategories = totalConditionCategories;
            ViewBag.ActiveConditionCategories = activeConditionCategories;
            ViewBag.InactiveConditionCategories = inactiveConditionCategories;

            ViewBag.TotalAllergies = totalAllergies;
            ViewBag.ActiveAllergies = activeAllergies;
            ViewBag.InactiveAllergies = inactiveAllergies;

            ViewBag.TotalAllergyCategories = totalAllergyCategories;
            ViewBag.ActiveAllergyCategories = activeAllergyCategories;
            ViewBag.InactiveAllergyCategories = inactiveAllergyCategories;

            ViewBag.TotalMedications = totalMedications;
            ViewBag.ActiveMedications = activeMedications;
            ViewBag.InactiveMedications = inactiveMedications;

            ViewBag.TotalMedicationCategories = totalMedicationCategories;
            ViewBag.ActiveMedicationCategories = activeMedicationCategories;
            ViewBag.InactiveMedicationCategories = inactiveMedicationCategories;

            return View();
        }

        #endregion
    }
}