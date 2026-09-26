namespace HemaCore.Models
{
    public class ConsentCondition
    {
        
            public int Id { get; set; }

            public int ConsentId { get; set; }

            public int ConditionId { get; set; }

            public Consent Consent { get; set; } = null!;

            public Condition Condition { get; set; } = null!;
        
    }
}
