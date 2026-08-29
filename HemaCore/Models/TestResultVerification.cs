//using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestResultVerification
    {
        [Key]
        public int TestResultVerificationId { get; set; }
        public int VerifiedByTechnicianId { get; set; }
        public DateTime VerificationDateTime { get; set; }
        public string Status { get; set; }
        public string ReviewNotes { get; set; }

        public int TestResultId { get; set; }
        public TestResult TestResult { get; set; } // Test result navigation
    }
}
