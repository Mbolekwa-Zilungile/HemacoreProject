using HemaCore.Models;

namespace HemaCore.ViewModels.ManagerVM
{
    public class ReassignTestTypeViewModel
    {
        public int TestTypeId { get; set; }
        public string TestTypeName { get; set; } = string.Empty;

        public List<LabTechnician> AvailableTechnicians { get; set; }
            = new();
    }
}
