namespace HemaCore.ViewModels.ManagerVM
{
    public class TestCategoryReportViewModel
    {

        public DateTime StartDate { get; set; } =
               DateTime.Today.AddDays(-30);

        public DateTime EndDate { get; set; } =
            DateTime.Today;

        public List<TestCategoryReportItem> Categories { get; set; }
            = new List<TestCategoryReportItem>();

        public int GrandTotal { get; set; }
    }
}
