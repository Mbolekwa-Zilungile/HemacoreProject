
using System;
using System.Collections.Generic;

namespace HemaCore.Models.ViewModels
{
    public class LabTechnicianDashboardViewModel
    {
        public string TechnicianName { get; set; } = string.Empty;

        // Sample receipt
        public int SamplesAwaitingReceipt { get; set; }

        // Processing workload
        public int TestsAwaitingProcessing { get; set; }
        public int MySelectedTests { get; set; }
        public int WaitingToBeSelected { get; set; }

        // Results management
        public int WaitingVerification { get; set; }
        public int WaitingReview { get; set; }

        // Priority and turnaround monitoring
        public int UrgentTests { get; set; }
        public int OverdueTests { get; set; }
        public int NearingTurnaround { get; set; }

        // Tests currently assigned to this technician
        public List<LabTechnicianWorkloadViewModel> Workload { get; set; }
            = new List<LabTechnicianWorkloadViewModel>();

        // Eligible tests displayed under the dashboard filters
        public List<LabTechnicianWorkloadViewModel> AvailableTests { get; set; }
            = new List<LabTechnicianWorkloadViewModel>();

        // Selected filter values
        public string? FilterUrgency { get; set; }
        public int? FilterCategoryId { get; set; }
        public string? FilterDueStatus { get; set; }
        public int? FilterRequestNumber { get; set; }
    }

    public class LabTechnicianWorkloadViewModel
    {
        // Specific test record ID, used to open TestDetails
        public int TestRequestTestTypeId { get; set; }

        public int TestRequestId { get; set; }

        public string PatientName { get; set; } = string.Empty;

        public string TestTypeName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string Urgency { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime? DueTime { get; set; }
    }
}
