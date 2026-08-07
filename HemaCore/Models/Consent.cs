namespace HemaCore.Models
{
    public class Consent
    {
        public int Id { get; set; }
        public string PatientId { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }

        public Users Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public ICollection<ConsentTestAccess> TestAccesses { get; set; } = [];
    }
}
