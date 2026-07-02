using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace HemaCore.Models
{
    public class LabTechnician
    {
        [Key]
        public int LabTechnicianId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, ErrorMessage = "Surname can't be longer than 50 characters")]
        public String Surname { get; set; }

        public bool IsActive { get; set; } = true;

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

        [BindNever]
        public string EmployeeNumber { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\+27\d{9}$", ErrorMessage = "Phone number must be in the format +27756455676")]
        public string PhoneNumber { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public Users User { get; set; } //User navigation 

        public ICollection<TechnicianTestType> TechnicianTests { get; set; } = new List<TechnicianTestType>();
        public ICollection<TestRequestTestType> TestRequestTestType = new List<TestRequestTestType>();

    }
}
