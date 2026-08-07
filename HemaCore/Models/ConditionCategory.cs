using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class ConditionCategory
    {
        public int Id { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public ICollection<Condition> Conditions { get; set; } = [];
    }
}
