
namespace HemaCore.Models
{
    public class ConsentRequest
    {
        public int Id { get; set; }

        // The patient's Users.Id
        public string PatientID { get; set; } = string.Empty;

        public int DoctorId { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        // Pending, Approved, or Rejected
        public string Status { get; set; } = "Pending";

        public DateTime? RespondedAt { get; set; }

        public Users Patient { get; set; } = null!;

        public Doctor Doctor { get; set; } = null!;
    }
}
