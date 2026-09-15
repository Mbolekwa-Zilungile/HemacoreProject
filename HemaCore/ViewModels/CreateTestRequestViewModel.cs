using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.DoctorViewModels
{
    public class CreateTestRequestViewModel
    {
        [Required(ErrorMessage = "Please select a patient")]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Please select an urgency level")]
        [Display(Name = "Urgency")]
        public string Urgency { get; set; } = string.Empty;

        [Display(Name = "Clinical Notes")]
        public string? ClinicalNotes { get; set; }

        [Display(Name = "Test Types")]
        public List<int> SelectedTestTypeIds { get; set; } = new();

        [Display(Name = "Sample Barcodes")]
        public List<string> BarcodeNumbers { get; set; } = new();

        [Display(Name = "Sample Types")]
        public List<int> SampleTypeIds { get; set; } = new();

        // Dropdown/checklist data — populated by the controller, not submitted by the user
        public List<PatientOptionViewModel> Patients { get; set; } = new();
        public List<TestTypeOptionViewModel> TestTypes { get; set; } = new();
        public List<SampleTypeOptionViewModel> SampleTypes { get; set; } = new();
    }

    public class PatientOptionViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class TestTypeOptionViewModel
    {
        public int TestTypeId { get; set; }
        public string TestTypeName { get; set; } = string.Empty;
    }

    public class SampleTypeOptionViewModel
    {
        public int SampleTypeId { get; set; }
        public string SampleName { get; set; } = string.Empty;
    }
}