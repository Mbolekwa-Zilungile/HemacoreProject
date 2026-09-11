using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestRequest
    {
        [Key]
        public int TestRequestId { get; set; }

        // Patient who the test request is for
        public int PatientId { get; set; }
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

        // Samples belonging to this request
        public ICollection<Sample> Samples { get; set; }
            = new List<Sample>();

        // Tests requested in this request
        public ICollection<TestRequestItem> TestRequestTestTypes { get; set; }
            = new List<TestRequestItem>();
    }
}