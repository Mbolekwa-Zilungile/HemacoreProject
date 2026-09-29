using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Condition
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
        [MaxLength(500)] public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public ConditionCategory Category { get; set; } = null!;
        public ICollection<PatientCondition> PatientConditions { get; set; } = new List<PatientCondition>();
        public ICollection<ConsentCondition> ConsentConditions { get; set; } = new List<ConsentCondition>();
    }
}
