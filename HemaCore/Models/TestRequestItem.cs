using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestRequestItem
    {
        [Key]
        public int TestRequestTestTypeId { get; set; }

        // Test Request
        public int TestRequestId { get; set; }
        public TestRequest TestRequest { get; set; }

        // Test Type
        public int TestTypeId { get; set; }
        public TestType TestType { get; set; }

        // Lab Technician assigned to perform the test
        public int? LabTechnicianId { get; set; }
        public LabTechnician LabTechnician { get; set; }

        // Test progress status
        public string Status { get; set; }

        // Test processing times
        public DateTime? StartDateTime { get; set; }
        public DateTime? CompletionDateTime { get; set; }

        // Test Result
        public int? TestResultId { get; set; }
        public TestResult TestResult { get; set; }
    }
}