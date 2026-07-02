using HemaCore.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Consumable
    {
        [Key]
        public int ConsumableId { get; set; }
        public string ConsumableName { get; set; }
        public int ReorderLevel { get; set; }

        public int QuantityInHand { get; set; }

        public ConsumableStatus Status
        {

            get
            {
                if (QuantityInHand == 0)
                    return ConsumableStatus.OutOfStock;

                if (QuantityInHand <= ReorderLevel)
                    return ConsumableStatus.LowStock;

                return ConsumableStatus.InStock;
            }

        }
        public ICollection<TestTypeConsumable> TestTypeConsumables { get; set; } = new List<TestTypeConsumable>();
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }//Supplier navigation property
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
