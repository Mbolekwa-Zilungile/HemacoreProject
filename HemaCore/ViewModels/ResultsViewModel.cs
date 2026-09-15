namespace HemaCore.ViewModels.DoctorViewModels
{
    public class ResultsViewModel
    {
        public int TestRequestId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<VerifiedTestResultViewModel> TestResults { get; set; } = new();
    }

    public class VerifiedTestResultViewModel
    {
        public string TestTypeName { get; set; } = string.Empty;
        public string UnitOfMeasurement { get; set; } = string.Empty;
        public double NormalRangeMin { get; set; }
        public double NormalRangeMax { get; set; }
        public string ResultValue { get; set; } = string.Empty;
        public bool IsAbnormal { get; set; }
        public string? Notes { get; set; }
    }
}