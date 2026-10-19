namespace HemaCore.ViewModels.ManagerVM
{
    public class TestCategoryReportItem
    {
        public string CategoryName { get; set; } = string.Empty;

        public int TotalTests { get; set; }

        public List<TestTypeReportItem> TestTypes { get; set; }
            = new();
    }
}
