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
                    "Cannot delete this category because it has test types assigned to it.";

                return RedirectToAction("TestCategories");
            }

            category.IsActive = false;
            _context.SaveChanges();

            TempData["Success"] = "Category deleted successfully.";

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

            var model = new TestTypeViewModel
            {
                Consumables = consumables.Select(c => new ConsumableVM
                {
                    ConsumableID = c.ConsumableId,
                    ConsumableName = c.ConsumableName,
                    IsSelected = false,
                    QuantityUsed = 0
                }).ToList()
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTestType(TestTypeViewModel model)
        {
            // =====================================================
            // LOAD DROPDOWN DATA
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
                return View(model);
            }


            // =====================================================
            // VALIDATE NORMAL RANGE
            // =====================================================

            if (model.NormalRangeMin > model.NormalRangeMax)
            {
                ModelState.AddModelError(
                    "NormalRangeMin",
                    "Minimum normal range cannot be greater than maximum normal range."
                );

                await ReloadConsumables(model);
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

                // CATEGORY
                CategoryId = model.CategoryID,

                // SAMPLE TYPE
                SampleTypeId = model.SampleTypeID,

                IsActive = true
            };


            _context.TestTypes.Add(testType);


            // =====================================================
            // SAVE
            // =====================================================

            await _context.SaveChangesAsync();


            TempData["Success"] =
                $"{model.TestTypeName} added successfully.";


            return RedirectToAction("TestTypes");
        }


        // =========================================================
        // RELOAD CONSUMABLES WHEN VALIDATION FAILS
        // =========================================================

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
                    "Cannot delete this sample type because it is being used by a test type.";

                return RedirectToAction("SampleTypes");
            }

            // Soft delete
            sample.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Sample type deleted successfully.";

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


        #endregion
    }
}

