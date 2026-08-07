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
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}
