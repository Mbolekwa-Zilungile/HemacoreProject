using HemaCore.Models;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.Patient
{
    public class EditPatientProfileViewModel
    {
        public string Email { get; set; } = "";

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Compare(nameof(Password))]
        [DataType(DataType.Password)]
        public string? ConfirmPassword { get; set; }

        [Required] public string Name { get; set; } = "";
        [Required] public string Surname { get; set; } = "";
        [Required] public string IDNumber { get; set; } = "";
        [Required] public string PhoneNumber { get; set; } = "";
        [Required] public string Gender { get; set; } = "";
        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }
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

        public List<int> SelectedConditionIds { get; set; } = new();
        public List<int> SelectedAllergyIds { get; set; } = new();
        public List<int> SelectedMedicationIds { get; set; } = new();

        public List<Condition> AvailableConditions { get; set; } = new();
        public List<Allergy> AvailableAllergies { get; set; } = new();
        public List<Medication> AvailableMedications { get; set; } = new();

        public List<ConditionCategory> ConditionCategories { get; set; } = new();
        public List<AllergyCategory> AllergyCategories { get; set; } = new();
        public List<MedicationCategory> MedicationCategories { get; set; } = new();
    }
}