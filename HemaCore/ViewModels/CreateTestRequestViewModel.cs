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
        public List<int?> SampleTypeIds { get; set; } = new();
        public List<PatientOptionViewModel> Patients { get; set; } = new();
        public List<TestTypeOptionViewModel> TestTypes { get; set; } = new();
        public List<SampleTypeOptionViewModel> SampleTypes { get; set; } = new();
    }

    public class PatientOptionViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;

        public string IDNumber { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        public List<string> Conditions { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public List<string> Medications { get; set; } = new();
    }

    public class TestTypeOptionViewModel
    {
        public int TestTypeId { get; set; }
        public string TestTypeName { get; set; } = string.Empty;
        public string UnitOfMeasurement { get; set; } = string.Empty;

        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public int? SampleTypeId { get; set; }
        public string SampleTypeName { get; set; } = string.Empty;
    }

    public class SampleTypeOptionViewModel
    {
        public int SampleTypeId { get; set; }
        public string SampleName { get; set; } = string.Empty;
    }
}