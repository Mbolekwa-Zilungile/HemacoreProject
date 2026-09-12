using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class AllergyCategory
    {
        public int Id { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public ICollection<Allergy> Allergies { get; set; } = [];
    }
}
