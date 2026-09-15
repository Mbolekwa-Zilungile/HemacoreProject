using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class PatientMedication
    {
        public int Id { get; set; }
        public string PatientID { get; set; } = string.Empty;
        public int MedicationId { get; set; }
        [MaxLength(100)] public string? Dosage { get; set; }
        [MaxLength(100)] public string? Frequency { get; set; }
        public Users Patient { get; set; } = null!;
        public Medication Medication { get; set; } = null!;
    }
}
