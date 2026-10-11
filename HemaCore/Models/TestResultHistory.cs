
using System;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestResultHistory
    {
        [Key]
        public int TestResultHistoryId { get; set; }

        // Individual test this history entry belongs to
        public int TestRequestTestTypeId { get; set; }

        public TestRequestItem TestRequestItem { get; set; } = null!;

        // Technician responsible for this action
        public int? LabTechnicianId { get; set; }

        public LabTechnician? LabTechnician { get; set; }

        // Example: Result Captured, Returned for Review,
        // Result Corrected, Resubmitted, Verified
        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;

        // Status before and after the event
        [StringLength(50)]
        public string? PreviousStatus { get; set; }

        [StringLength(50)]
        public string? NewStatus { get; set; }

        // Snapshot of the result at the time of the event
        public string? ResultValue { get; set; }

        public string? ResultNotes { get; set; }

        public bool? IsAbnormal { get; set; }

        // Review or verification note
        public string? ReviewNotes { get; set; }

        public DateTime ActionDateTime { get; set; }
    }
}
