namespace HemaCore.ViewModels.ManagerVM
{
    public class ReassignTechnicianViewModel
    {
          public int TechnicianId { get; set; }
        public string TechnicianName { get; set; } = string.Empty;

        public List<ReassignTestTypeViewModel> TestTypes { get; set; }
            = new();
    }
}
