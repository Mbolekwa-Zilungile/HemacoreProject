using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace HemaCore.Models
{
    public class Doctor
    {
        public int Id { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string Surname { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Email { get; set; } = string.Empty;
        [MaxLength(50)] public string? PracticeNumber { get; set; }

        public ICollection<TestRequest> TestRequests { get; set; } = [];
        public ICollection<Consent> ConsentsReceived { get; set; } = [];
        public ICollection<TestResult> VerifiedResults { get; set; } = [];
    
}
}
