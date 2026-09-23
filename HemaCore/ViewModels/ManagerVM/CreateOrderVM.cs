namespace HemaCore.ViewModels.ManagerVM
{
    public class CreateOrderVM
    {
        public int SupplierID { get; set; }

        public List<CreateOrderItemVM> Items { get; set; }
            = new List<CreateOrderItemVM>();
    }
}
