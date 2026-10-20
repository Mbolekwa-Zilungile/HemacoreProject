using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required.")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(9, ErrorMessage = "Phone number cannot exceed 9 digits.")]
        [RegularExpression(@"^\d{0,10}$", ErrorMessage = "Phone number must contain only digits and a maximum of 9 digits.")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}