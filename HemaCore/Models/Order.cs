using HemaCore.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public OrderStatus Status { get; set; }

        public DateTime? DateCompleted { get; set; }
        public DateTime? DateCancelled { get; set; }
        public string CancelReason { get; set; }

        public int SupplierId { get; set; }

        public Supplier Supplier { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }= new List<OrderItem>();   
    }
}
