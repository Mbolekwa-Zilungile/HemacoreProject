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

        public AdminController(AppDbContext context, UserManager<Users> userManager, SignInManager<Users> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }
        #region Index

        // GET: Admin
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

            var conditionRecords = await _context.PatientConditions
                .CountAsync();

            var allergyRecords = await _context.PatientAllergies
                .CountAsync();

            var medicationRecords = await _context.PatientMedications
                .CountAsync();

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
        // GET: Admin/DashboardStats
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

            var conditionRecords = await _context.PatientConditions
                .CountAsync();

            var allergyRecords = await _context.PatientAllergies
                .CountAsync();

            var medicationRecords = await _context.PatientMedications
                .CountAsync();

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

        #region Condition
        // GET: Admin/Conditions
        public async Task<IActionResult> Conditions()
        {
            // IMPORTANT:
            // Do NOT filter by IsActive here.
            // We want both Active and Inactive conditions to remain visible.
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
                    x => x.Count
                );

            // Categories themselves can still be active only.
            var categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.ConditionCount = conditions.Count;
            ViewBag.ConditionCountByCategory = conditionCountByCategory;
            ViewBag.Categories = categories;

            return View(conditions);
        }
        // GET: Admin/AddCondition
        public async Task<IActionResult> AddCondition()
        {
            var categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            if (!categories.Any())
            {
                TempData["Error"] = "Please create a condition category first before adding conditions.";
                return RedirectToAction(nameof(ConditionCategories));
            }

            ViewBag.Categories = categories;
            return View(new Condition());
        }

        // POST: Admin/AddCondition
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCondition(Condition condition)
        {
            // Remove navigation properties from validation to avoid issues
            ModelState.Remove("Category");
            ModelState.Remove("PatientConditions");

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if condition with same name exists in the category (case insensitive)
                    var exists = await _context.Conditions
                        .AnyAsync(c => c.Name.ToLower() == condition.Name.Trim().ToLower() &&
                                      c.CategoryId == condition.CategoryId &&
                                      c.IsActive);

                    if (exists)
                    {
                        TempData["Error"] = "A condition with this name already exists in the selected category.";
                        ViewBag.Categories = await _context.ConditionCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();
                        return View(condition);
                    }

                    condition.IsActive = true;
                    condition.Name = condition.Name.Trim();
                    if (condition.Description != null)
                    {
                        condition.Description = condition.Description.Trim();
                    }

                    _context.Conditions.Add(condition);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Condition added successfully!";
                    return RedirectToAction(nameof(Conditions));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "An error occurred while saving: " + ex.Message;
                    ViewBag.Categories = await _context.ConditionCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();
                    return View(condition);
                }
            }

            // If ModelState is invalid, reload categories
            ViewBag.Categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(condition);
        }

        // GET: Admin/EditCondition/{id}
        public async Task<IActionResult> EditCondition(int id)
        {
            var condition = await _context.Conditions
                .Include(c => c.Category)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (condition == null)
            {
                TempData["Error"] = "Condition not found.";
                return RedirectToAction(nameof(Conditions));
            }

            ViewBag.Categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(condition);
        }

        // POST: Admin/EditCondition/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCondition(int id, Condition condition)
        {
            if (id != condition.Id)
            {
                TempData["Error"] = "Invalid condition ID.";
                return RedirectToAction(nameof(Conditions));
            }

            // Remove navigation properties from validation
            ModelState.Remove("Category");
            ModelState.Remove("PatientConditions");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Conditions.FindAsync(id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Condition not found.";
                        return RedirectToAction(nameof(Conditions));
                    }

                    // Check for duplicate name in same category (excluding current)
                    var duplicate = await _context.Conditions
                        .AnyAsync(c => c.Name.ToLower() == condition.Name.Trim().ToLower() &&
                                      c.CategoryId == condition.CategoryId &&
                                      c.Id != id &&
                                      c.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] = "A condition with this name already exists in the selected category.";
                        ViewBag.Categories = await _context.ConditionCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();
                        return View(condition);
                    }

                    existing.Name = condition.Name.Trim();
                    existing.Description = condition.Description?.Trim();
                    existing.CategoryId = condition.CategoryId;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Condition updated successfully!";
                    return RedirectToAction(nameof(Conditions));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await ConditionExists(condition.Id))
                    {
                        TempData["Error"] = "Condition not found.";
                        return RedirectToAction(nameof(Conditions));
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "An error occurred while updating: " + ex.Message;
                }
            }

            ViewBag.Categories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(condition);
        }

        // GET: Admin/DeleteCondition/{id}
        public async Task<IActionResult> DeleteCondition(int id)
        {
            var condition = await _context.Conditions
                .Include(c => c.Category)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (condition == null)
            {
                TempData["Error"] = "Condition not found.";
                return RedirectToAction(nameof(Conditions));
            }

            // Check if condition is being used by any patient
            var isUsed = await _context.PatientConditions
                .AnyAsync(pc => pc.ConditionId == id);

            if (isUsed)
            {
                TempData["Error"] = "This condition cannot be deleted as it is being used by patients. You can deactivate it instead.";
                return RedirectToAction(nameof(Conditions));
            }

            return View(condition);
        }

        // POST: Admin/DeleteCondition/{id}
        [HttpPost, ActionName("DeleteCondition")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConditionConfirmed(int id)
        {
            var condition = await _context.Conditions.FindAsync(id);
            if (condition != null)
            {
                // Soft delete - just deactivate
                condition.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Condition deactivated successfully!";
            }

            return RedirectToAction(nameof(Conditions));
        }

        // POST: Admin/ToggleConditionStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleConditionStatus(int id)
        {
            var condition = await _context.Conditions
                .FirstOrDefaultAsync(c => c.Id == id);

            if (condition == null)
            {
                TempData["Error"] = "Condition not found.";
                return RedirectToAction(nameof(Conditions));
            }

            // Toggle the current status
            condition.IsActive = !condition.IsActive;

            await _context.SaveChangesAsync();

            if (condition.IsActive)
            {
                TempData["Success"] = "Condition activated successfully.";
            }
            else
            {
                TempData["Success"] = "Condition deactivated successfully.";
            }

            return RedirectToAction(nameof(Conditions));
        }

        private async Task<bool> ConditionExists(int id)
        {
            return await _context.Conditions.AnyAsync(e => e.Id == id);
        }

        // GET: Admin/SearchConditionsLive
        public async Task<IActionResult> SearchConditionsLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allConditions = await _context.Conditions
                    .Include(c => c.Category)
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Category.Name)
                    .ThenBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        categoryName = c.Category.Name,
                        description = c.Description,
                        isActive = c.IsActive
                    })
                    .ToListAsync();
                return Json(allConditions);
            }

            var conditions = await _context.Conditions
                .Include(c => c.Category)
                .Where(c => c.IsActive &&
                           (c.Name.Contains(term) ||
                            (c.Description != null && c.Description.Contains(term)) ||
                            c.Category.Name.Contains(term)))
                .OrderBy(c => c.Category.Name)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    categoryName = c.Category.Name,
                    description = c.Description,
                    isActive = c.IsActive
                })
                .ToListAsync();

            return Json(conditions);
        }


        // GET: Admin/ConditionCategories
        // GET: Admin/ConditionCategories
        public async Task<IActionResult> ConditionCategories()
        {
            var categories = await _context.ConditionCategories
                .Include(c => c.Conditions)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var conditionCountByCategory = await _context.Conditions
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

            ViewBag.CategoryCount = categories.Count;
            ViewBag.ConditionCountByCategory = conditionCountByCategory;

            return View(categories);
        }

        // GET: Admin/AddConditionCategory
        public IActionResult AddConditionCategory()
        {
            return View(new ConditionCategory());
        }

        // POST: Admin/AddConditionCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConditionCategory(ConditionCategory category)
        {
            if (ModelState.IsValid)
            {
                // Check if category with same name exists (case insensitive)
                var exists = await _context.ConditionCategories
                    .AnyAsync(c => c.Name.ToLower() == category.Name.Trim().ToLower() && c.IsActive);

                if (exists)
                {
                    TempData["Error"] = "A category with this name already exists.";
                    return View(category);
                }

                category.IsActive = true;
                category.Name = category.Name.Trim();
                _context.ConditionCategories.Add(category);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Condition category added successfully!";
                return RedirectToAction(nameof(Conditions)); // Redirects to Conditions page
            }

            return View(category);
        }

        // GET: Admin/EditConditionCategory/{id}
        public async Task<IActionResult> EditConditionCategory(int id)
        {
            var category = await _context.ConditionCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(ConditionCategories));
            }

            return View(category);
        }

        // POST: Admin/EditConditionCategory/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConditionCategory(int id, ConditionCategory category)
        {
            if (id != category.Id)
            {
                TempData["Error"] = "Invalid category ID.";
                return RedirectToAction(nameof(ConditionCategories));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.ConditionCategories.FindAsync(id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Category not found.";
                        return RedirectToAction(nameof(ConditionCategories));
                    }

                    // Check for duplicate name (excluding current)
                    var duplicate = await _context.ConditionCategories
                        .AnyAsync(c => c.Name.ToLower() == category.Name.Trim().ToLower() &&
                                      c.Id != id &&
                                      c.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] = "A category with this name already exists.";
                        return View(category);
                    }

                    existing.Name = category.Name.Trim();
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Category updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await CategoryExists(category.Id))
                    {
                        TempData["Error"] = "Category not found.";
                        return RedirectToAction(nameof(ConditionCategories));
                    }
                    throw;
                }
                return RedirectToAction(nameof(ConditionCategories));
            }

            return View(category);
        }

        // GET: Admin/DeleteConditionCategory/{id}
        public async Task<IActionResult> DeleteConditionCategory(int id)
        {
            var category = await _context.ConditionCategories
                .Include(c => c.Conditions)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(ConditionCategories));
            }

            // Check if category has active conditions
            var hasActiveConditions = await _context.Conditions
                .AnyAsync(c => c.CategoryId == id && c.IsActive);

            if (hasActiveConditions)
            {
                TempData["Error"] = "This category cannot be deleted as it has active conditions. Please deactivate or move the conditions first.";
                return RedirectToAction(nameof(ConditionCategories));
            }

            return View(category);
        }

        // POST: Admin/DeleteConditionCategory/{id}
        [HttpPost, ActionName("DeleteConditionCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConditionCategoryConfirmed(int id)
        {
            var category = await _context.ConditionCategories.FindAsync(id);
            if (category != null)
            {
                // Soft delete - just deactivate
                category.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Category deactivated successfully!";
            }

            return RedirectToAction(nameof(ConditionCategories));
        }

        // POST: Admin/ToggleCategoryStatus/{id}
        // POST: Admin/ToggleCategoryStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCategoryStatus(int id)
        {
            var category = await _context.ConditionCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Condition category not found.";
                return RedirectToAction(nameof(ConditionCategories));
            }

            if (category.IsActive)
            {
                var hasActiveConditions = await _context.Conditions
                    .AnyAsync(c => c.CategoryId == id && c.IsActive);

                if (hasActiveConditions)
                {
                    TempData["Error"] =
                        "Cannot deactivate this category because it still contains active conditions. Please deactivate or move the conditions first.";

                    return RedirectToAction(nameof(ConditionCategories));
                }
            }

            category.IsActive = !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = category.IsActive
                ? "Condition category activated successfully!"
                : "Condition category deactivated successfully!";

            return RedirectToAction(nameof(ConditionCategories));
        }

        // GET: Admin/SearchCategoriesLive
        public async Task<IActionResult> SearchCategoriesLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allCategories = await _context.ConditionCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        conditionCount = c.Conditions.Count(cd => cd.IsActive),
                        isActive = c.IsActive
                    })
                    .ToListAsync();
                return Json(allCategories);
            }

            var categories = await _context.ConditionCategories
                .Where(c => c.IsActive && c.Name.Contains(term))
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    conditionCount = c.Conditions.Count(cd => cd.IsActive),
                    isActive = c.IsActive
                })
                .ToListAsync();

            return Json(categories);
        }

        private async Task<bool> CategoryExists(int id)
        {
            return await _context.ConditionCategories.AnyAsync(e => e.Id == id);
        }
        #endregion

        #region Allergy
        // GET: Admin/Allergies
        // GET: Admin/Allergies
        public async Task<IActionResult> Allergies()
        {
            // IMPORTANT:
            // Do NOT filter by IsActive here.
            // We want both Active and Inactive allergies to remain visible.
            var allergies = await _context.Allergies
                .Include(a => a.Category)
                .OrderBy(a => a.Category.Name)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var allergyCountByCategory = await _context.Allergies
                .GroupBy(a => a.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(
                    x => x.CategoryId,
                    x => x.Count
                );

            // Categories themselves can still be active only.
            var categories = await _context.AllergyCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.AllergyCount = allergies.Count;
            ViewBag.AllergyCountByCategory = allergyCountByCategory;
            ViewBag.Categories = categories;

            return View(allergies);
        }
        // GET: Admin/AddAllergy
        public async Task<IActionResult> AddAllergy()
        {
            var categories = await _context.AllergyCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            if (!categories.Any())
            {
                TempData["Error"] = "Please create an allergy category first before adding allergies.";
                return RedirectToAction(nameof(AllergyCategories));
            }

            ViewBag.Categories = categories;
            return View(new Allergy());
        }

        // POST: Admin/AddAllergy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAllergy(Allergy allergy)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Category");
            ModelState.Remove("PatientAllergies");

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if an active allergy with the same name
                    // already exists in the selected category
                    var exists = await _context.Allergies
                        .AnyAsync(a =>
                            a.Name.ToLower() == allergy.Name.Trim().ToLower() &&
                            a.CategoryId == allergy.CategoryId &&
                            a.IsActive);

                    if (exists)
                    {
                        TempData["Error"] =
                            "An allergy with this name already exists in the selected category.";

                        ViewBag.Categories = await _context.AllergyCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();

                        return View(allergy);
                    }

                    // New allergies are active by default
                    allergy.IsActive = true;

                    // Clean the input
                    allergy.Name = allergy.Name.Trim();
                    allergy.Description = allergy.Description.Trim();

                    _context.Allergies.Add(allergy);

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Allergy added successfully!";

                    return RedirectToAction(nameof(Allergies));
                }
                catch (Exception ex)
                {
                    TempData["Error"] =
                        "An error occurred while saving: " + ex.Message;

                    ViewBag.Categories = await _context.AllergyCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(allergy);
                }
            }

            // ModelState invalid - reload categories
            ViewBag.Categories = await _context.AllergyCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(allergy);
        }

        // GET: Admin/EditAllergy/{id}
        public async Task<IActionResult> EditAllergy(int id)
        {
            var allergy = await _context.Allergies
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allergy == null)
            {
                TempData["Error"] = "Allergy not found.";
                return RedirectToAction(nameof(Allergies));
            }

            ViewBag.Categories = await _context.AllergyCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(allergy);
        }

        // POST: Admin/EditAllergy/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAllergy(
            int id,
            Allergy allergy)
        {
            if (id != allergy.Id)
            {
                TempData["Error"] =
                    "Invalid allergy ID.";

                return RedirectToAction(nameof(Allergies));
            }

            // Remove navigation properties from validation
            ModelState.Remove("Category");
            ModelState.Remove("PatientAllergies");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Allergies
                        .FirstOrDefaultAsync(a => a.Id == id);

                    if (existing == null)
                    {
                        TempData["Error"] =
                            "Allergy not found.";

                        return RedirectToAction(nameof(Allergies));
                    }

                    // Check for duplicate active allergy
                    var duplicate = await _context.Allergies
                        .AnyAsync(a =>
                            a.Name.ToLower() ==
                            allergy.Name.Trim().ToLower()
                            &&
                            a.CategoryId ==
                            allergy.CategoryId
                            &&
                            a.Id != id
                            &&
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

                    // Update all editable fields
                    existing.Name =
                        allergy.Name.Trim();

                    existing.Description =
                        allergy.Description.Trim();

                    existing.CategoryId =
                        allergy.CategoryId;

                    // IMPORTANT:
                    // We do NOT change IsActive here.
                    // Editing an inactive allergy should keep it inactive.

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Allergy updated successfully!";

                    return RedirectToAction(nameof(Allergies));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await AllergyExists(allergy.Id))
                    {
                        TempData["Error"] =
                            "Allergy not found.";

                        return RedirectToAction(nameof(Allergies));
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

            // Reload categories if validation fails
            ViewBag.Categories =
                await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(allergy);
        }
        // GET: Admin/DeleteAllergy/{id}
        public async Task<IActionResult> DeleteAllergy(int id)
        {
            var allergy = await _context.Allergies
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allergy == null)
            {
                TempData["Error"] = "Allergy not found.";
                return RedirectToAction(nameof(Allergies));
            }

            // Check if allergy is being used by any patient
            var isUsed = await _context.PatientAllergies
                .AnyAsync(pa => pa.AllergyId == id);

            if (isUsed)
            {
                TempData["Error"] = "This allergy cannot be deleted as it is being used by patients. You can deactivate it instead.";
                return RedirectToAction(nameof(Allergies));
            }

            return View(allergy);
        }

        // POST: Admin/DeleteAllergy/{id}
        [HttpPost, ActionName("DeleteAllergy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllergyConfirmed(int id)
        {
            var allergy = await _context.Allergies.FindAsync(id);
            if (allergy != null)
            {
                // Soft delete - just deactivate
                allergy.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Allergy deactivated successfully!";
            }

            return RedirectToAction(nameof(Allergies));
        }

        // POST: Admin/ToggleAllergyStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAllergyStatus(int id)
        {
            var allergy = await _context.Allergies
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allergy == null)
            {
                TempData["Error"] = "Allergy not found.";
                return RedirectToAction(nameof(Allergies));
            }

            // Toggle the current status
            allergy.IsActive = !allergy.IsActive;

            await _context.SaveChangesAsync();

            if (allergy.IsActive)
            {
                TempData["Success"] = "Allergy activated successfully.";
            }
            else
            {
                TempData["Success"] = "Allergy deactivated successfully.";
            }

            return RedirectToAction(nameof(Allergies));
        }
        private async Task<bool> AllergyExists(int id)
        {
            return await _context.Allergies.AnyAsync(e => e.Id == id);
        }

        // GET: Admin/SearchAllergiesLive
        public async Task<IActionResult> SearchAllergiesLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allAllergies = await _context.Allergies
                    .Include(a => a.Category)
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.Category.Name)
                    .ThenBy(a => a.Name)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        categoryName = a.Category.Name,
                        description = a.Description,
                        isActive = a.IsActive
                    })
                    .ToListAsync();
                return Json(allAllergies);
            }

            var allergies = await _context.Allergies
                .Include(a => a.Category)
                .Where(a => a.IsActive &&
                           (a.Name.Contains(term) ||
                            a.Category.Name.Contains(term)))
                .OrderBy(a => a.Category.Name)
                .ThenBy(a => a.Name)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    categoryName = a.Category.Name,
                    isActive = a.IsActive
                })
                .ToListAsync();

            return Json(allergies);
        }

        // GET: Admin/AllergyCategories
        // GET: Admin/AllergyCategories
        public async Task<IActionResult> AllergyCategories()
        {
            var categories = await _context.AllergyCategories
                .Include(c => c.Allergies)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var allergyCountByCategory = await _context.Allergies
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

            ViewBag.CategoryCount = categories.Count;
            ViewBag.AllergyCountByCategory = allergyCountByCategory;

            return View(categories);
        }

        // GET: Admin/AddAllergyCategory
        public IActionResult AddAllergyCategory()
        {
            return View(new AllergyCategory());
        }

        // POST: Admin/AddAllergyCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAllergyCategory(AllergyCategory category)
        {
            if (ModelState.IsValid)
            {
                // Check if category with same name exists (case insensitive)
                var exists = await _context.AllergyCategories
                    .AnyAsync(c => c.Name.ToLower() == category.Name.Trim().ToLower() && c.IsActive);

                if (exists)
                {
                    TempData["Error"] = "A category with this name already exists.";
                    return View(category);
                }

                category.IsActive = true;
                category.Name = category.Name.Trim();
                _context.AllergyCategories.Add(category);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Allergy category added successfully!";
                return RedirectToAction(nameof(Allergies));
            }

            return View(category);
        }

        // GET: Admin/EditAllergyCategory/{id}
        public async Task<IActionResult> EditAllergyCategory(int id)
        {
            var category = await _context.AllergyCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(AllergyCategories));
            }

            return View(category);
        }

        // POST: Admin/EditAllergyCategory/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAllergyCategory(int id, AllergyCategory category)
        {
            if (id != category.Id)
            {
                TempData["Error"] = "Invalid category ID.";
                return RedirectToAction(nameof(AllergyCategories));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.AllergyCategories.FindAsync(id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Category not found.";
                        return RedirectToAction(nameof(AllergyCategories));
                    }

                    // Check for duplicate name (excluding current)
                    var duplicate = await _context.AllergyCategories
                        .AnyAsync(c => c.Name.ToLower() == category.Name.Trim().ToLower() &&
                                      c.Id != id &&
                                      c.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] = "A category with this name already exists.";
                        return View(category);
                    }

                    existing.Name = category.Name.Trim();
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Category updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await AllergyCategoryExists(category.Id))
                    {
                        TempData["Error"] = "Category not found.";
                        return RedirectToAction(nameof(AllergyCategories));
                    }
                    throw;
                }
                return RedirectToAction(nameof(AllergyCategories));
            }

            return View(category);
        }

        // GET: Admin/DeleteAllergyCategory/{id}
        public async Task<IActionResult> DeleteAllergyCategory(int id)
        {
            var category = await _context.AllergyCategories
                .Include(c => c.Allergies)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(AllergyCategories));
            }

            // Check if category has active allergies
            var hasActiveAllergies = await _context.Allergies
                .AnyAsync(a => a.CategoryId == id && a.IsActive);

            if (hasActiveAllergies)
            {
                TempData["Error"] = "This category cannot be deleted as it has active allergies. Please deactivate or move the allergies first.";
                return RedirectToAction(nameof(AllergyCategories));
            }

            return View(category);
        }

        // POST: Admin/DeleteAllergyCategory/{id}
        [HttpPost, ActionName("DeleteAllergyCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllergyCategoryConfirmed(int id)
        {
            var category = await _context.AllergyCategories.FindAsync(id);
            if (category != null)
            {
                // Soft delete - just deactivate
                category.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Category deactivated successfully!";
            }

            return RedirectToAction(nameof(AllergyCategories));
        }

        // POST: Admin/ToggleAllergyCategoryStatus/{id}
        // POST: Admin/ToggleAllergyCategoryStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAllergyCategoryStatus(int id)
        {
            var category = await _context.AllergyCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Allergy category not found.";
                return RedirectToAction(nameof(AllergyCategories));
            }

            if (category.IsActive)
            {
                var hasActiveAllergies = await _context.Allergies
                    .AnyAsync(a => a.CategoryId == id && a.IsActive);

                if (hasActiveAllergies)
                {
                    TempData["Error"] =
                        "Cannot deactivate this category because it still contains active allergies. Please deactivate or move the allergies first.";

                    return RedirectToAction(nameof(AllergyCategories));
                }
            }

            category.IsActive = !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = category.IsActive
                ? "Allergy category activated successfully!"
                : "Allergy category deactivated successfully!";

            return RedirectToAction(nameof(AllergyCategories));
        }

        // GET: Admin/SearchAllergyCategoriesLive
        public async Task<IActionResult> SearchAllergyCategoriesLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allCategories = await _context.AllergyCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        allergyCount = c.Allergies.Count(a => a.IsActive),
                        isActive = c.IsActive
                    })
                    .ToListAsync();
                return Json(allCategories);
            }

            var categories = await _context.AllergyCategories
                .Where(c => c.IsActive && c.Name.Contains(term))
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    allergyCount = c.Allergies.Count(a => a.IsActive),
                    isActive = c.IsActive
                })
                .ToListAsync();

            return Json(categories);
        }

        private async Task<bool> AllergyCategoryExists(int id)
        {
            return await _context.AllergyCategories.AnyAsync(e => e.Id == id);
        }
        #endregion

        #region Medication
        // GET: Admin/Medications
        // GET: Admin/Medications
        public async Task<IActionResult> Medications()
        {
            // IMPORTANT:
            // Do NOT filter by IsActive here.
            // We want both Active and Inactive medications to remain visible.
            var medications = await _context.Medications
                .Include(m => m.Category)
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            var medicationCountByCategory = await _context.Medications
                .GroupBy(m => m.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(
                    x => x.CategoryId,
                    x => x.Count
                );

            // Categories themselves can still be active only.
            var categories = await _context.MedicationCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.MedicationCount = medications.Count;
            ViewBag.MedicationCountByCategory = medicationCountByCategory;
            ViewBag.Categories = categories;

            return View(medications);
        }
        // GET: Admin/AddMedication
        public async Task<IActionResult> AddMedication()
        {
            var categories = await _context.MedicationCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            if (!categories.Any())
            {
                TempData["Error"] = "Please create a medication category first before adding medications.";
                return RedirectToAction(nameof(MedicationCategories));
            }

            ViewBag.Categories = categories;
            return View(new Medication());
        }

        // POST: Admin/AddMedication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMedication(
            Medication medication)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Category");
            ModelState.Remove("PatientMedications");

            if (ModelState.IsValid)
            {
                try
                {
                    // Check for duplicate active medication
                    var exists = await _context.Medications
                        .AnyAsync(m =>
                            m.Name.ToLower() ==
                            medication.Name.Trim().ToLower()
                            &&
                            m.CategoryId ==
                            medication.CategoryId
                            &&
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

                    // New medication is active
                    medication.IsActive = true;

                    // Clean input
                    medication.Name =
                        medication.Name.Trim();

                    medication.Description =
                        medication.Description.Trim();

                    _context.Medications.Add(medication);

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Medication added successfully!";

                    return RedirectToAction(nameof(Medications));
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

            // ModelState invalid
            ViewBag.Categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(medication);
        }

        // GET: Admin/EditMedication/{id}
        public async Task<IActionResult> EditMedication(int id)
        {
            var medication = await _context.Medications
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medication == null)
            {
                TempData["Error"] = "Medication not found.";
                return RedirectToAction(nameof(Medications));
            }

            ViewBag.Categories = await _context.MedicationCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(medication);
        }

        // POST: Admin/EditMedication/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMedication(
            int id,
            Medication medication)
        {
            if (id != medication.Id)
            {
                TempData["Error"] =
                    "Invalid medication ID.";

                return RedirectToAction(nameof(Medications));
            }

            // Remove navigation properties from validation
            ModelState.Remove("Category");
            ModelState.Remove("PatientMedications");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing =
                        await _context.Medications
                            .FirstOrDefaultAsync(m => m.Id == id);

                    if (existing == null)
                    {
                        TempData["Error"] =
                            "Medication not found.";

                        return RedirectToAction(nameof(Medications));
                    }

                    // Check for duplicate active medication
                    var duplicate =
                        await _context.Medications
                            .AnyAsync(m =>
                                m.Name.ToLower() ==
                                medication.Name.Trim().ToLower()
                                &&
                                m.CategoryId ==
                                medication.CategoryId
                                &&
                                m.Id != id
                                &&
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

                    // Update all editable fields
                    existing.Name =
                        medication.Name.Trim();

                    existing.Description =
                        medication.Description.Trim();

                    existing.CategoryId =
                        medication.CategoryId;

                    // IMPORTANT:
                    // Do not change IsActive when editing.

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Medication updated successfully!";

                    return RedirectToAction(nameof(Medications));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await MedicationExists(medication.Id))
                    {
                        TempData["Error"] =
                            "Medication not found.";

                        return RedirectToAction(nameof(Medications));
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

            // Reload categories
            ViewBag.Categories =
                await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            return View(medication);
        }
        // GET: Admin/DeleteMedication/{id}
        public async Task<IActionResult> DeleteMedication(int id)
        {
            var medication = await _context.Medications
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medication == null)
            {
                TempData["Error"] = "Medication not found.";
                return RedirectToAction(nameof(Medications));
            }

            // Check if medication is being used by any patient
            var isUsed = await _context.PatientMedications
                .AnyAsync(pm => pm.MedicationId == id);

            if (isUsed)
            {
                TempData["Error"] = "This medication cannot be deleted as it is being used by patients. You can deactivate it instead.";
                return RedirectToAction(nameof(Medications));
            }

            return View(medication);
        }

        // POST: Admin/DeleteMedication/{id}
        [HttpPost, ActionName("DeleteMedication")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMedicationConfirmed(int id)
        {
            var medication = await _context.Medications.FindAsync(id);
            if (medication != null)
            {
                // Soft delete - just deactivate
                medication.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Medication deactivated successfully!";
            }

            return RedirectToAction(nameof(Medications));
        }

        // POST: Admin/ToggleMedicationStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMedicationStatus(int id)
        {
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medication == null)
            {
                TempData["Error"] = "Medication not found.";
                return RedirectToAction(nameof(Medications));
            }

            // Toggle the current status
            medication.IsActive = !medication.IsActive;

            await _context.SaveChangesAsync();

            if (medication.IsActive)
            {
                TempData["Success"] = "Medication activated successfully.";
            }
            else
            {
                TempData["Success"] = "Medication deactivated successfully.";
            }

            return RedirectToAction(nameof(Medications));
        }

        private async Task<bool> MedicationExists(int id)
        {
            return await _context.Medications.AnyAsync(e => e.Id == id);
        }

        // GET: Admin/SearchMedicationsLive
        public async Task<IActionResult> SearchMedicationsLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allMedications = await _context.Medications
                    .Include(m => m.Category)
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.Category.Name)
                    .ThenBy(m => m.Name)
                    .Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        categoryName = m.Category.Name,
                        description = m.Description,
                        isActive = m.IsActive
                    })
                    .ToListAsync();
                return Json(allMedications);
            }

            var medications = await _context.Medications
                .Include(m => m.Category)
                .Where(m => m.IsActive &&
                           (m.Name.Contains(term) ||
                            m.Category.Name.Contains(term)))
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    categoryName = m.Category.Name,
                    isActive = m.IsActive
                })
                .ToListAsync();

            return Json(medications);
        }

        // GET: Admin/MedicationCategories
        // GET: Admin/MedicationCategories
        public async Task<IActionResult> MedicationCategories()
        {
            var categories = await _context.MedicationCategories
                .Include(c => c.Medications)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var medicationCountByCategory = await _context.Medications
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

            ViewBag.CategoryCount = categories.Count;
            ViewBag.MedicationCountByCategory = medicationCountByCategory;

            return View(categories);
        }

        // GET: Admin/AddMedicationCategory
        public IActionResult AddMedicationCategory()
        {
            return View(new MedicationCategory());
        }

        // POST: Admin/AddMedicationCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMedicationCategory(MedicationCategory category)
        {
            if (ModelState.IsValid)
            {
                // Check if category with same name exists (case insensitive)
                var exists = await _context.MedicationCategories
                    .AnyAsync(c => c.Name.ToLower() == category.Name.Trim().ToLower() && c.IsActive);

                if (exists)
                {
                    TempData["Error"] = "A category with this name already exists.";
                    return View(category);
                }

                category.IsActive = true;
                category.Name = category.Name.Trim();
                _context.MedicationCategories.Add(category);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Medication category added successfully!";
                return RedirectToAction(nameof(Medications));
            }

            return View(category);
        }

        // GET: Admin/EditMedicationCategory/{id}
        public async Task<IActionResult> EditMedicationCategory(int id)
        {
            var category = await _context.MedicationCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(MedicationCategories));
            }

            return View(category);
        }

        // POST: Admin/EditMedicationCategory/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMedicationCategory(int id, MedicationCategory category)
        {
            if (id != category.Id)
            {
                TempData["Error"] = "Invalid category ID.";
                return RedirectToAction(nameof(MedicationCategories));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.MedicationCategories.FindAsync(id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Category not found.";
                        return RedirectToAction(nameof(MedicationCategories));
                    }

                    // Check for duplicate name (excluding current)
                    var duplicate = await _context.MedicationCategories
                        .AnyAsync(c => c.Name.ToLower() == category.Name.Trim().ToLower() &&
                                      c.Id != id &&
                                      c.IsActive);

                    if (duplicate)
                    {
                        TempData["Error"] = "A category with this name already exists.";
                        return View(category);
                    }

                    existing.Name = category.Name.Trim();
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Category updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await MedicationCategoryExists(category.Id))
                    {
                        TempData["Error"] = "Category not found.";
                        return RedirectToAction(nameof(MedicationCategories));
                    }
                    throw;
                }
                return RedirectToAction(nameof(MedicationCategories));
            }

            return View(category);
        }

        // GET: Admin/DeleteMedicationCategory/{id}
        public async Task<IActionResult> DeleteMedicationCategory(int id)
        {
            var category = await _context.MedicationCategories
                .Include(c => c.Medications)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(MedicationCategories));
            }

            // Check if category has active medications
            var hasActiveMedications = await _context.Medications
                .AnyAsync(m => m.CategoryId == id && m.IsActive);

            if (hasActiveMedications)
            {
                TempData["Error"] = "This category cannot be deleted as it has active medications. Please deactivate or move the medications first.";
                return RedirectToAction(nameof(MedicationCategories));
            }

            return View(category);
        }

        // POST: Admin/DeleteMedicationCategory/{id}
        [HttpPost, ActionName("DeleteMedicationCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMedicationCategoryConfirmed(int id)
        {
            var category = await _context.MedicationCategories.FindAsync(id);
            if (category != null)
            {
                // Soft delete - just deactivate
                category.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Category deactivated successfully!";
            }

            return RedirectToAction(nameof(MedicationCategories));
        }

        // POST: Admin/ToggleMedicationCategoryStatus/{id}
        // POST: Admin/ToggleMedicationCategoryStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMedicationCategoryStatus(int id)
        {
            var category = await _context.MedicationCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Medication category not found.";
                return RedirectToAction(nameof(MedicationCategories));
            }

            if (category.IsActive)
            {
                var hasActiveMedications = await _context.Medications
                    .AnyAsync(m => m.CategoryId == id && m.IsActive);

                if (hasActiveMedications)
                {
                    TempData["Error"] =
                        "Cannot deactivate this category because it still contains active medications. Please deactivate or move the medications first.";

                    return RedirectToAction(nameof(MedicationCategories));
                }
            }

            category.IsActive = !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = category.IsActive
                ? "Medication category activated successfully!"
                : "Medication category deactivated successfully!";

            return RedirectToAction(nameof(MedicationCategories));
        }
        // GET: Admin/SearchMedicationCategoriesLive
        public async Task<IActionResult> SearchMedicationCategoriesLive(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                var allCategories = await _context.MedicationCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        medicationCount = c.Medications.Count(m => m.IsActive),
                        isActive = c.IsActive
                    })
                    .ToListAsync();
                return Json(allCategories);
            }

            var categories = await _context.MedicationCategories
                .Where(c => c.IsActive && c.Name.Contains(term))
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    medicationCount = c.Medications.Count(m => m.IsActive),
                    isActive = c.IsActive
                })
                .ToListAsync();

            return Json(categories);
        }

        private async Task<bool> MedicationCategoryExists(int id)
        {
            return await _context.MedicationCategories.AnyAsync(e => e.Id == id);
        }
        #endregion

        #region Account Management

        // GET: Admin/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProfileViewModel
            {
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        // POST: Admin/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check if email is already taken by another user
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError("Email", "This email is already taken by another user.");
                return View(model);
            }

            // Update user details
            user.Email = model.Email;
            user.UserName = model.Email; // Update username to match email
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: Admin/ChangePassword
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST: Admin/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check if current password is correct
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
            if (!isPasswordValid)
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            // Change password
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                // Update security stamp to invalidate existing sessions
                await _userManager.UpdateSecurityStampAsync(user);

                TempData["Success"] = "Password changed successfully!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        #endregion


    }

}
