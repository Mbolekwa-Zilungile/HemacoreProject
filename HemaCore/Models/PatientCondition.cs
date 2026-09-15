namespace HemaCore.Models
{
    public class PatientCondition
    {
        public int Id { get; set; }
        public string PatientID { get; set; } = string.Empty;
        public int ConditionId { get; set; }
        public DateOnly? DiagnosedDate { get; set; }
        public Users Patient { get; set; } = null!;
        public Condition Condition { get; set; } = null!;
    }
}
