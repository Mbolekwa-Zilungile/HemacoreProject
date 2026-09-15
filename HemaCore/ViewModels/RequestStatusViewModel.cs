namespace HemaCore.ViewModels.DoctorViewModels
{
    public class RequestStatusViewModel
    {
        public int TestRequestId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string Urgency { get; set; } = string.Empty;
        public string? CancelReason { get; set; }
        public DateTime? CancellationDate { get; set; }
        public List<TestItemStatusViewModel> TestItems { get; set; } = new();
        public List<SampleStatusViewModel> Samples { get; set; } = new();
    }

    public class TestItemStatusViewModel
    {
        public string TestTypeName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class SampleStatusViewModel
    {
        public string Barcode { get; set; } = string.Empty;
        public string SampleTypeName { get; set; } = string.Empty;
    }
}