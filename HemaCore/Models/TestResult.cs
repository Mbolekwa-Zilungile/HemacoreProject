
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestResult
    {
        [Key]
        public int TestResultId { get; set; }

        public string ResultValue { get; set; }

        public string Notes { get; set; }

        public bool IsAbnormal { get; set; }

        public DateTime DateCaptured { get; set; }

        public ICollection<TestRequestItem> TestRequestTestType { get; set; }
            = new List<TestRequestItem>(); // Test request item navigation

        public TestResultVerification TestResultVerification { get; set; } // Test result verification navigation
    }
}
`
