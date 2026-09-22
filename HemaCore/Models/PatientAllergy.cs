using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class PatientAllergy
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int AllergyId { get; set; }
        [MaxLength(20)] public string? Severity { get; set; } // mild | moderate | severe
        public Patient Patient { get; set; } = null!;
        public Allergy Allergy { get; set; } = null!;
    }
}
