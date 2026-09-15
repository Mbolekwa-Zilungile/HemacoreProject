using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.DoctorViewModels
{
    public class PatientListItemViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class PatientDetailsViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public List<string> Conditions { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public List<string> Medications { get; set; } = new();
    }

    public class AddPatientViewModel
    {
        [Required, Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required, Display(Name = "Surname")]
        public string Surname { get; set; } = string.Empty;

        [Required, Display(Name = "ID Number")]
        public string IDNumber { get; set; } = string.Empty;

        [Required, Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required, Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }

        public List<int> SelectedConditionIds { get; set; } = new();
        public List<int> SelectedAllergyIds { get; set; } = new();
        public List<int> SelectedMedicationIds { get; set; } = new();

        public List<CheckOptionViewModel> AllConditions { get; set; } = new();
        public List<CheckOptionViewModel> AllAllergies { get; set; } = new();
        public List<CheckOptionViewModel> AllMedications { get; set; } = new();
    }

    public class EditPatientHistoryViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;

        public List<int> SelectedConditionIds { get; set; } = new();
        public List<int> SelectedAllergyIds { get; set; } = new();
        public List<int> SelectedMedicationIds { get; set; } = new();

        public List<CheckOptionViewModel> AllConditions { get; set; } = new();
        public List<CheckOptionViewModel> AllAllergies { get; set; } = new();
        public List<CheckOptionViewModel> AllMedications { get; set; } = new();
    }

    public class CheckOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}