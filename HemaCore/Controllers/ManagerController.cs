using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.Enums;
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
            var testtype =  await _context.TestTypes
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
                .Where(c=> c.IsActive)
                .OrderBy(t => t.SampleName)
                .ToListAsync();
            return View(sampletype);
        }
        #endregion

        #region Consumable

        public async Task<IActionResult> Consumables()
        {
            var consumables = await _context.Consumables
                .Include(c=> c.Supplier)
                .Where(c=> c.IsActive)
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
                .OrderByDescending(c=> c.OrderId)
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
                .Where(x => x.IsActive)
                .OrderBy(x=> x.Surname) 
                .ThenBy(x => x.Name)
                .ToListAsync()  ;

            return View(doctor);
        }

        #endregion
    }
}
