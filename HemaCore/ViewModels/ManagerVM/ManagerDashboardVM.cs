using HemaCore.Models;

namespace HemaCore.ViewModels.ManagerVM
{
    public class ManagerDashboardVM
    {

        public List<Consumable> LowStockConsumables { get; set; } = new();
        public List<Consumable> OutOfStockConsumables { get; set; } = new();
        public List<Consumable> ConsumablesWithoutSupplier { get; set; } = new();

        public List<TestType> TestTypesWithoutTechnician { get; set; } = new();

        public List<Order> RecentOrders { get; set; } = new();

        public int PendingOrdersCount { get; set; }
        public int TestCategoryCount { get; set; }
        public int TestTypeCount { get; set; }
        public int DoctorCount { get; set; }
        public int TechnicianCount { get; set; }

        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int TotalStockAlerts { get; set; }

        public int ConsumablesWithoutSupplierCount =>
            ConsumablesWithoutSupplier.Count;

        public int TestTypesWithoutTechnicianCount =>
            TestTypesWithoutTechnician.Count;
    }
}

