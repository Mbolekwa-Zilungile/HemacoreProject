using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemaCore.Models
{
    public class Patient
    {
        [Key]
        public int PatientID { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Surname { get; set; }

        [Required]
        public  string Gender{get; set; }

        [Required]
        public  string IDNumber { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [Display(Name = "Phone Number")]
        [RegularExpression(
    @"^\+27\s?\d{2}\s?\d{3}\s?\d{4}$",
    ErrorMessage = "Enter a valid South African phone number in +27 format (e.g. +27 82 123 4567 or +27821234567).")]
        public string PhoneNumber { get; set; }

        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(150)]
        public string StreetAddress { get; set; }

        [Required]
        [StringLength(100)]
        public string Suburb { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [Required]
        [StringLength(100)]
        public string Province { get; set; }
        [Required]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must contain 4 digits")]
        public string PostalCode { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public Users User { get; set; }
    }
}
