
namespace HemaCore.Models
{
    public class ConsentMedication
    {
        public int Id { get; set; }

        public int ConsentId { get; set; }

        public int MedicationId { get; set; }

        public Consent Consent { get; set; } = null!;

        public Medication Medication { get; set; } = null!;
    }
}

