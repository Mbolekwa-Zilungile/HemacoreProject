using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.DoctorViewModels
{
    public class DoctorProfileViewModel
    {
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, ErrorMessage = "Surname can't be longer than 50 characters")]
        public string Surname { get; set; }

        [StringLength(50)]
        public string HPCSANumber { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(
            @"^\+27\d{9}$",
            ErrorMessage = "Phone number must be in the format +27756455676")]
        public string PhoneNumber { get; set; }

        // Read-only in the view — Identity email changes go through
        // a separate, more careful flow later, not this form.
        public string Email { get; set; }
    }
}