namespace HemaCore.ViewModels.ManagerVM
{
    public class ConsumableVM
    {
        public int ConsumableID { get; set; }

        public string? ConsumableName { get; set; }

        public bool IsSelected { get; set; }

        public int QuantityUsed { get; set; }
    }
}
