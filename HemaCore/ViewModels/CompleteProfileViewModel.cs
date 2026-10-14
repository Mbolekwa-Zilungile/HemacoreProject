using HemaCore.Models;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.Patient
{
    public class CompleteProfileViewModel
    {
        [Required] public string Name { get; set; } = "";
        [Required] public string Surname { get; set; } = "";
        [Required] public string IDNumber { get; set; } = "";
        [Required] public string Gender { get; set; } = "";

        [Required]
        [RegularExpression(@"^\+27\d{9}$", ErrorMessage = "Phone must be +27XXXXXXXXX")]
        public string PhoneNumber { get; set; } = "";

        [Required] public DateTime DateOfBirth { get; set; }
        [Required(ErrorMessage = "Street address is required")]
        [StringLength(150)]
        [Display(Name = "Street Address")]
        public string StreetAddress { get; set; } = "";

        [Required(ErrorMessage = "Suburb is required")]
        [StringLength(100)]
        public string Suburb { get; set; } = "";

        [Required(ErrorMessage = "City or town is required")]
        [StringLength(100)]
        public string City { get; set; } = "";

        [Required(ErrorMessage = "Please select a province")]
        public string Province { get; set; } = "";

        [Required(ErrorMessage = "Postal code is required")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must contain 4 digits")]
        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; } = "";

        // Selected reference-data IDs
        public List<int> SelectedConditionIds { get; set; } = new();
        public List<int> SelectedAllergyIds { get; set; } = new();
        public List<int> SelectedMedicationIds { get; set; } = new();

        // Custom "Other" entries (free text)
        public string? OtherCondition { get; set; }
        public string? OtherAllergy { get; set; }
        public string? OtherMedication { get; set; }

        // For rendering the multi-select lists
        public List<Condition> AvailableConditions { get; set; } = new();
        public List<Allergy> AvailableAllergies { get; set; } = new();
        public List<Medication> AvailableMedications { get; set; } = new();
    }
}