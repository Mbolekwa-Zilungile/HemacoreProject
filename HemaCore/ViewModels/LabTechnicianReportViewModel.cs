namespace HemaCore.Models.ViewModels
{
    public class LabTechnicianReportViewModel
    {
        // Report filters
        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public string TestType { get; set; }

        public string Status { get; set; }

        public string Urgency { get; set; }


        // Report summary
        public int TotalTests { get; set; }

        public int CompletedTests { get; set; }

        public int InProgressTests { get; set; }

        public int WaitingVerification { get; set; }

        public int AbnormalResults { get; set; }


        // Report data
        public List<LabTechnicianReportItemViewModel> Tests { get; set; }
            = new List<LabTechnicianReportItemViewModel>();
    }


    public class LabTechnicianReportItemViewModel
    {
        public int TestRequestId { get; set; }

        public string PatientName { get; set; }

        public string TestTypeName { get; set; }

        public string TechnicianName { get; set; }

        public string Urgency { get; set; }

        public string Status { get; set; }

        public string ResultValue { get; set; }

        public bool IsAbnormal { get; set; }

        public DateTime? StartDateTime { get; set; }

        public DateTime? CompletionDateTime { get; set; }

        public double? TurnaroundMinutes { get; set; }
    }
}