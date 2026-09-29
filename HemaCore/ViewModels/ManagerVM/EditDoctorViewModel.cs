using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.ManagerVM
{
    public class EditDoctorViewModel
    {
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, ErrorMessage = "Surname can't be longer than 50 characters")]
        public string Surname { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(
            @"^\+27\d{9}$",
            ErrorMessage = "Phone number must be in the format +27756455676"
        )]
        public string PhoneNumber { get; set; }

        [StringLength(50)]
        public string HPCSANumber { get; set; }

        public bool IsActive { get; set; }
    }
}
