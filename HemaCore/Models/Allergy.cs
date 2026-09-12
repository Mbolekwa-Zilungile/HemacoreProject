using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Allergy
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public AllergyCategory Category { get; set; } = null!;
        public ICollection<PatientAllergy> PatientAllergies { get; set; } = [];
    }
}
