using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace HemaCore.ViewModels.ManagerVM
{
    public class TechnicianViewModel : IValidatableObject
    {
        public int LabTechnicianId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters")]
        public string Name { get; set; }


        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, ErrorMessage = "Surname can't be longer than 50 characters")]
        public string Surname { get; set; }

        [Required(ErrorMessage = "ID Number is required")]
        public string IDNumber { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(IDNumber))
            {
                if (IDNumber.Length != 13 || !long.TryParse(IDNumber, out _))
                {
                    yield return new ValidationResult(
                        "ID Number must be 13 digits",
                        new[] { nameof(IDNumber) });
                }
                else
                {
                    string datePart = IDNumber.Substring(0, 6);
                    if (!DateTime.TryParseExact(datePart, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime birthDate))
                    {
                        yield return new ValidationResult(
                            "Invalid birthdate in ID Number",
                            new[] { nameof(IDNumber) });
                    }
                    else if (birthDate > DateTime.Today)
                    {
                        yield return new ValidationResult(
                            "Birthdate cannot be in the future",
                            new[] { nameof(IDNumber) });
                    }
                }
            }
        }

        public string? EmployeeNumber { get; set; }

        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(
     12,
     MinimumLength = 1,
     ErrorMessage = "Phone number cannot exceed 12 characters.")]
        [RegularExpression(
     @"^\+?[0-9]{1,11}$",
     ErrorMessage = "Phone number must contain only digits and an optional leading + sign, with a maximum of 12 characters.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        public List<int> SelectedTestTypeIds { get; set; } = new();

        public List<TestTypeViewModel> TestTypes { get; set; } = new();
    }
}