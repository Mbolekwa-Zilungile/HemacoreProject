
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.DoctorViewModels
{
    public class PatientListItemViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the patient's phone number.")]
        [RegularExpression(
            @"^\+27\d{9}$",
            ErrorMessage = "Enter a valid South African phone number, e.g. +27712345678."
        )]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public int RequestCount { get; set; }

        public List<string> Conditions { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public List<string> Medications { get; set; } = new();
    }

    public class PatientDetailsViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Address { get; set; }

        public List<string> Conditions { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public List<string> Medications { get; set; } = new();

        public List<PatientRequestSummaryViewModel> Requests { get; set; } = new();
    }

    public class PatientRequestSummaryViewModel
    {
        public int TestRequestId { get; set; }
        public DateTime RequestDate { get; set; }
        public string Urgency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
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

        [Required(ErrorMessage = "Please enter the patient's phone number.")]
        [RegularExpression(
            @"^\+27\d{9}$",
            ErrorMessage = "Enter a valid South African phone number, e.g. +27712345678."
        )]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Gender is required")]
        [Display(Name = "Gender")]
        public string Gender { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        // Combined address string saved to the database.
        [Display(Name = "Address")]
        public string? Address { get; set; }

        // South African address fields.
        [Required(ErrorMessage = "Street address is required.")]
        [Display(Name = "Street Address")]
        public string StreetAddress { get; set; } = string.Empty;

        [Display(Name = "Suburb")]
        public string? Suburb { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [Display(Name = "City")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a province.")]
        [RegularExpression(
            @"^(Eastern Cape|Free State|Gauteng|KwaZulu-Natal|Limpopo|Mpumalanga|Northern Cape|North West|Western Cape)$",
            ErrorMessage = "Please select a valid South African province."
        )]
        [Display(Name = "Province")]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "Postal code is required.")]
        [RegularExpression(
            @"^\d{4}$",
            ErrorMessage = "Postal code must be 4 digits."
        )]
        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; } = string.Empty;

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

        // Use DateOnly? because the PatientCondition model uses DateOnly?.
        public Dictionary<int, DateOnly?> ConditionDiagnosedDates { get; set; } = new();

        public Dictionary<int, string?> AllergySeverities { get; set; } = new();

        public Dictionary<int, string?> MedicationDosages { get; set; } = new();

        public Dictionary<int, string?> MedicationFrequencies { get; set; } = new();
    }

    public class CheckOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
