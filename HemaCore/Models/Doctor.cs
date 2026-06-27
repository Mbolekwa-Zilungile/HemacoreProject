using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemaCore.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorID { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, ErrorMessage = "Surname can't be longer than 50 characters")]
        public string Surname { get; set; }

        public bool IsActive { get; set; }

        [Required]
        [StringLength(30)]
        public string HPCSANumber { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\+27\d{9}$", ErrorMessage = "Phone number must be in the format +27756455676")]
        public string PhoneNumber { get; set; }
        public string FullName => $"{Name} {Surname}";

        //User model
        [Required]
        public string UserId { get; set; }
        
        [ForeignKey(nameof(UserId))]
        public Users User { get; set;}
    }
}
