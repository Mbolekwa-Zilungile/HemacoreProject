namespace HemaCore.ViewModels.DoctorViewModels
{
    public class ReportsViewModel
    {
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public List<ReportRequestItemViewModel> Requests { get; set; } = new();
    }

    public class ReportRequestItemViewModel
    {
        public int TestRequestId { get; set; }
        public DateTime RequestDate { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Urgency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}