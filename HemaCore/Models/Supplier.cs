using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string ContactPerson { get; set; }

        [EmailAddress]
        public string Email { get; set; }
        public bool IsActive { get; set; }

        public ICollection<Consumable> Consumables { get; set; }
           = new List<Consumable>();

    }
}
