namespace HemaCore.ViewModels.ManagerVM
{
    public class StockAdjustmentVM
    {
        public int ConsumableID { get; set; }

        public string ConsumableName { get; set; }

        public int QuantityOnHand { get; set; }

        public int AdjustmentQuantity { get; set; }

        public string AdjustmentType { get; set; }

        public string Reason { get; set; }
    }
}
