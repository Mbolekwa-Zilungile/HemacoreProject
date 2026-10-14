using System;
using System.Collections.Generic;

namespace HemaCore.Models.ViewModels
{
    public class LabTechnicianDashboardViewModel
    {
        public string TechnicianName { get; set; } = string.Empty;

        // Sample receipt
        public int SamplesAwaitingReceipt { get; set; }

        // Existing processing count
        public int TestsAwaitingProcessing { get; set; }

        // Required dashboard workload indicators
        public int MySelectedTests { get; set; }

        public int WaitingToBeSelected { get; set; }

        public int WaitingVerification { get; set; }

        public int WaitingReview { get; set; }

        public int UrgentTests { get; set; }

        public int OverdueTests { get; set; }

        public int NearingTurnaround { get; set; }

        // Workload records for dashboard tables or summaries
        public List<LabTechnicianWorkloadViewModel> Workload { get; set; }
            = new List<LabTechnicianWorkloadViewModel>();
    }

    public class LabTechnicianWorkloadViewModel
    {
        public int TestRequestId { get; set; }

        public string PatientName { get; set; } = string.Empty;

        public string TestTypeName { get; set; } = string.Empty;

        public string Urgency { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime? DueTime { get; set; }
    }
}