
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels
{
    public class LabTechnicianProfileViewModel
    {
        [Required(ErrorMessage = "Please enter your name.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your surname.")]
        [Display(Name = "Surname")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }
}
