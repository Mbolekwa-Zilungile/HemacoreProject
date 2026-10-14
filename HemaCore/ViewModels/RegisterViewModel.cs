using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace HemaCore.ViewModels
{
    public class RegisterViewModel :IValidatableObject
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Surname { get; set; }

        [Required(ErrorMessage = "Please select your gender")]
        public string Gender { get; set; }

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

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [RegularExpression(@"^\+27\d{9}$", ErrorMessage = "Phone must be +27XXXXXXXXX")]
        public string PhoneNumber { get; set; }

        
        [Required(ErrorMessage = "Street address is required")]
        [StringLength(150)]
        [Display(Name = "Street Address")]
        public string StreetAddress { get; set; }

        [Required(ErrorMessage = "Suburb is required")]
        [StringLength(100)]
        public string Suburb { get; set; }

        [Required(ErrorMessage = "City or town is required")]
        [StringLength(100)]
        public string City { get; set; }

        [Required(ErrorMessage = "Please select a province")]
        public string Province { get; set; }

        [Required(ErrorMessage = "Postal code is required")]
        [RegularExpression(@"^\d{4}$",
            ErrorMessage = "Postal code must contain 4 digits")]
        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; }
    }
}
