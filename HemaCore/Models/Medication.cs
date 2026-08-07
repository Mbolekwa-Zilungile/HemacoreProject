using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Medication
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public MedicationCategory Category { get; set; } = null!;
        public ICollection<PatientMedication> PatientMedications { get; set; } = [];
    }
}
