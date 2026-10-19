namespace HemaCore.ViewModels.ManagerVM
{
    public class TestsPerformedReportViewModel
    {
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime EndDate { get; set; } = DateTime.Today;

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public int GrandTotal { get; set; }

        public List<TestCategoryReportItem> Categories { get; set; }
            = new();
    }
}
