using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.Enums;
using HemaCore.ViewModels.ManagerVM;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Controllers
{
    public class ManagerController : Controller
    {
        private AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public ManagerController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public IActionResult Index()
        {
            return View();
        }

        #region Test Catagories
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

            // =========================
            // UPDATE ITEM
            // =========================

            item.Status = OrderItemStatus.Received;
            item.DateReceived = DateTime.Now;

            // =========================
            // UPDATE STOCK
            // =========================

            var consumable = await _context.Consumables
                .FirstOrDefaultAsync(c => c.ConsumableId == item.ConsumableId);

            if (consumable != null)
            {
                consumable.QuantityInHand += item.Quantity;
            }

            // =========================
            // UPDATE ORDER STATUS
            // =========================

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

            // =========================
            // SAVE CHANGES
            // =========================

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




            TempData["Success"] =
                $"Doctor {name} {surname} was added successfully. " +
                $"HPCSA Number: {hpcsaNumber}. " +
                $"Temporary password: {temporaryPassword}";


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

            TempData["Success"] =
                $"Technician {name} {surname} was added successfully. " +
                $"Employee Number: {employeeNumber}.\n " +
                $"Temporary password: {temporaryPassword}";

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

