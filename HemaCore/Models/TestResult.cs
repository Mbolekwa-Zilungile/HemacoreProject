using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestResult
    {
        [Key]
        public int TestResultId {  get; set; }
        public string ResultValue { get; set; }
        public string Notes { get; set; }
        public bool IsAbnormal { get; set; }
        public DateTime DateCaptured {  get; set; }

        public ICollection<TestRequestItem> TestRequestTestType = new List<TestRequestItem>();//Test request test type navigation
        public TestResultVerification TestResultVerification { get; set; }// Test result verification navigation
    }
}
