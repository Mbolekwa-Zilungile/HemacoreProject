
namespace HemaCore.Models
{
    public class Consent
    {
        public int Id { get; set; }

        // This is the Users.Id
        public string PatientID { get; set; } = string.Empty;

        public int DoctorId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        // Consent automatically expires after this date/time
        public DateTime? ExpiresAt { get; set; }

        // Filled when patient manually revokes consent
        public DateTime? RevokedAt { get; set; }

        public Users Patient { get; set; } = null!;

        public Doctor Doctor { get; set; } = null!;

        public ICollection<ConsentCondition> Conditions { get; set; } = new List<ConsentCondition>();

        public ICollection<ConsentAllergy> Allergies { get; set; } = new List<ConsentAllergy>();

        public ICollection<ConsentMedication> Medications { get; set; } = new List<ConsentMedication>();

        public ICollection<ConsentTestRequest> TestAccesses { get; set; } = new List<ConsentTestRequest>();
    }
}

