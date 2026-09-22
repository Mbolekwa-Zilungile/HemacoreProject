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
        public DateTime DateOfBirth { get; set; }
        public string? Address { get; set; }

        public List<int> SelectedConditionIds { get; set; } = new();
        public List<int> SelectedAllergyIds { get; set; } = new();
        public List<int> SelectedMedicationIds { get; set; } = new();

        public List<Condition> AvailableConditions { get; set; } = new();
        public List<Allergy> AvailableAllergies { get; set; } = new();
        public List<Medication> AvailableMedications { get; set; } = new();
    }
}