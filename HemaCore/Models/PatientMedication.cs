using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class PatientMedication
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int MedicationId { get; set; }
        [MaxLength(100)] public string? Dosage { get; set; }
        [MaxLength(100)] public string? Frequency { get; set; }
        public Patient Patient { get; set; } = null!;
        public Medication Medication { get; set; } = null!;
    }
}
