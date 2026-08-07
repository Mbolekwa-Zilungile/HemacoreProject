namespace HemaCore.Models
{
    public class ConsentTestAccess
    {
        public int Id { get; set; }
        public int ConsentId { get; set; }
        public int TestRequestId { get; set; }
        public Consent Consent { get; set; } = null!;
        public TestRequest TestRequest { get; set; } = null!;
    }
}
