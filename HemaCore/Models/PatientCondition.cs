namespace HemaCore.Models
{
    public class PatientCondition
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int ConditionId { get; set; }
        public DateOnly? DiagnosedDate { get; set; }
        public Patient Patient { get; set; } = null!;
        public Condition Condition { get; set; } = null!;
    }
}
