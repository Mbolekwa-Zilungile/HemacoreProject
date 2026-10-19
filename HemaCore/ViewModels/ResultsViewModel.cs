using System;
using System.Collections.Generic;

namespace HemaCore.ViewModels.DoctorViewModels
{
    public class ResultsViewModel
    {
        // ---------- Existing fields ----------
        public int TestRequestId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<VerifiedTestResultViewModel> TestResults { get; set; } = new();

        // ---------- New fields for the release workflow ----------

        /// <summary>
        /// Date the request was submitted by the doctor.
        /// </summary>
        public DateTime RequestDate { get; set; }

        /// <summary>
        /// True when the doctor has already released results to the patient.
        /// </summary>
        public bool IsReleased { get; set; }

        /// <summary>
        /// When the doctor released the results (null if not released yet).
        /// </summary>
        public DateTime? ReleasedToPatientDate { get; set; }

        /// <summary>
        /// Optional note the doctor left when releasing the results.
        /// </summary>
        public string? ReleaseNote { get; set; }

        /// <summary>
        /// Number of test items in this request that are fully verified.
        /// </summary>
        public int VerifiedCount { get; set; }

        /// <summary>
        /// Number of test items in this request that are not yet verified.
        /// </summary>
        public int PendingCount { get; set; }
    }

    public class VerifiedTestResultViewModel
    {
        // ---------- Existing fields ----------
        public string TestTypeName { get; set; } = string.Empty;
        public string UnitOfMeasurement { get; set; } = string.Empty;
        public double NormalRangeMin { get; set; }
        public double NormalRangeMax { get; set; }
        public string ResultValue { get; set; } = string.Empty;
        public bool IsAbnormal { get; set; }
        public string? Notes { get; set; }

        // ---------- New fields (optional, non-breaking) ----------

        /// <summary>
        /// Identity of the individual test item (useful for future links).
        /// </summary>
        public int TestRequestTestTypeId { get; set; }

        /// <summary>
        /// When the lab technician verified this result.
        /// </summary>
        public DateTime? VerifiedOn { get; set; }
    }
}