using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class MedicationCategory
    {
        public int Id { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public ICollection<Medication> Medications { get; set; } = [];
    }
}
