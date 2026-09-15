using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestRequest
    {
        [Key]
        public int TestRequestId { get; set; }

        // Patient who the test request is for
        public int PatientID { get; set; }
        public Patient Patient { get; set; }

        // Doctor who submitted the request
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }

        // Request details
        public DateTime RequestDate { get; set; }

        public string Urgency { get; set; }

        public string ClinicalNotes { get; set; }

        // Overall request status
        public string Status { get; set; }

        // Cancellation details (set only if Status == "Cancelled")
        public string? CancelReason { get; set; }
        public DateTime? CancellationDate { get; set; }

        // Release details (set only when doctor releases results to patient)
        public string? ReleaseNote { get; set; }
        public DateTime? ReleasedToPatientDate { get; set; }

        // Samples belonging to this request
        public ICollection<Sample> Samples { get; set; }
            = new List<Sample>();

        // Tests requested in this request
        public ICollection<TestRequestItem> TestRequestTestTypes { get; set; }
            = new List<TestRequestItem>();
    }
}