namespace HemaCore.ViewModels.DoctorViewModels
{
    public class DashboardViewModel
    {
        public string DoctorName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int PendingRequests { get; set; }
        public int UrgentRequests { get; set; }
        public List<RecentRequestViewModel> RecentRequests { get; set; } = new();
    }

    public class MyRequestsViewModel
    {
        public string? StatusFilter { get; set; }
        public List<RecentRequestViewModel> Requests { get; set; } = new();
    }

    public class RecentRequestViewModel
    {
        public int TestRequestId { get; set; }
        public DateTime RequestDate { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Urgency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}