
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.ManagerVM
{
    public class ManagerProfileVM
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(
            @"^(?:(?:\+27|27)[6-8][0-9]{8}|0[6-8][0-9]{8})$",
            ErrorMessage = "Enter a valid South African mobile number, e.g. 0821234567 or +27821234567."
        )]
        [StringLength(12, ErrorMessage = "Phone Number is too long.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        public string? ConfirmNewPassword { get; set; }
    }
}
