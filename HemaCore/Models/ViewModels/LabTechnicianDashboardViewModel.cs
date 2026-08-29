namespace HemaCore.Models.ViewModels
{
    public class LabTechnicianDashboardViewModel
    {
        public int MySelectedTests { get; set; }

        public int WaitingToBeSelected { get; set; }

        public int WaitingVerification { get; set; }

        public int WaitingReview { get; set; }

        public int UrgentTests { get; set; }

        public int OverdueTests { get; set; }

        public int NearingTurnaround { get; set; }

        public List<LabTechnicianWorkloadViewModel> Workload { get; set; }
            = new List<LabTechnicianWorkloadViewModel>();
    }


    public class LabTechnicianWorkloadViewModel
    {
        public int TestRequestId { get; set; }

        public string PatientName { get; set; }

        public string TestTypeName { get; set; }

        public string Urgency { get; set; }

        public string Status { get; set; }

        public DateTime? DueTime { get; set; }
    }
}