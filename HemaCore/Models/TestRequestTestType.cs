namespace HemaCore.Models
{
    public class TestRequestTestType
    {
        public int LabTechnicianId { get; set; }
        public LabTechnician LabTechnician { get; set; }

        public int TestResultId { get; set; }
        public TestResult TestResult { get; set; }
    }
}
