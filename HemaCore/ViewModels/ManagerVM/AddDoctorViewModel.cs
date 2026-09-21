using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.ManagerVM
{
    public class AddDoctorViewModel
    {
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;


        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(
            @"^\+27\d{9}$",
            ErrorMessage = "Phone number must be in the format +27756455676")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;


        // ==========================
        // DOCTOR INFORMATION
        // ==========================

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters")]
        public string Name { get; set; } = string.Empty;


        [Required(ErrorMessage = "Surname is required.")]
        [StringLength(50, ErrorMessage = "Surname can't be longer than 50 characters")]
        public string Surname { get; set; } = string.Empty;


        [Required(ErrorMessage = "HPCSA number is required.")]
        [Display(Name = "HPCSA Number")]
        public string HPCSANumber { get; set; } = string.Empty;
    }
}
