namespace HemaCore.Models
{
    public class Alert
    {
        public int AlertId { get; set; }
        public int DoctorID { get; set; }
        public int TestRequestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public decimal? ResultValue { get; set; }
        public decimal? NormalRangeMin { get; set; }
        public decimal? NormalRangeMax { get; set; }
        public string UnitOfMeasurement { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}