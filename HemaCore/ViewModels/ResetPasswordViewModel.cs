using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a new password.")]
        [DataType(DataType.Password)]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage = "The password must be at least 6 characters long.")]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare(
            nameof(Password),
            ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}