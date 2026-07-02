namespace HemaCore.Models
{
    public class TestTypeSample
    {
        public int TestTypeId { get; set; }
        public TestType TestType { get; set; }// Test Type navigation

        public int SampleTypeId { get; set; }
        public SampleType SampleType { get; set; }// Sample type navigation
    }
}
