using System;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Sample
    {
        [Key]
        public int SampleId { get; set; }

        [Required]
        [StringLength(100)]
        public string Barcode { get; set; } = string.Empty;

        public DateTime? DateTimeReceived { get; set; }

        public int TestRequestId { get; set; }
        public TestRequest TestRequest { get; set; } = null!;

        public int SampleTypeId { get; set; }
        public SampleType SampleType { get; set; } = null!;

        public int? LabTechnicianId { get; set; }
        public LabTechnician? LabTechnician { get; set; }

        // Sample status: Pending, Received, or Rejected
        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        // Required when a sample is rejected
        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        public DateTime? RejectedDateTime { get; set; }

        public int? RejectedByLabTechnicianId { get; set; }
    }
}