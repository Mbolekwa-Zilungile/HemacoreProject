namespace HemaCore.ViewModels.DoctorViewModels
{
    public class AlertsViewModel
    {
        public int SelectedDays { get; set; }
        public int UnreadCount { get; set; }
        public List<AlertItemViewModel> Alerts { get; set; } = new();
    }

    public class AlertItemViewModel
    {
        public int AlertId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public decimal? ResultValue { get; set; }
        public decimal? NormalRangeMin { get; set; }
        public decimal? NormalRangeMax { get; set; }
        public string UnitOfMeasurement { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedDateTime { get; set; }
    }
}