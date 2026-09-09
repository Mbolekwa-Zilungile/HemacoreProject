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
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;


        // ==========================
        // DOCTOR INFORMATION
        // ==========================

        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        public string Name { get; set; } = string.Empty;


        [Required(ErrorMessage = "Surname is required.")]
        public string Surname { get; set; } = string.Empty;


        [Required(ErrorMessage = "HPCSA number is required.")]
        [Display(Name = "HPCSA Number")]
        public string HPCSANumber { get; set; } = string.Empty;
    }
}
