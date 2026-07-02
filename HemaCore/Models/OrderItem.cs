using HemaCore.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }

        public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;

        public DateTime? DateReceived { get; set; }
        public DateTime? DateRejected { get; set; }
        public string RejectReason { get; set; }

        public int ConsumableId { get; set; }
        public Consumable Consumable { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; }
    }
}
