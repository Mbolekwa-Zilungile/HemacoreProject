using HemaCore.Models;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.ManagerVM
{
    public class AssignTechnicianViewModel
    {
        public int TestTypeId { get; set; }

        public string TestTypeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select at least one technician.")]
        public List<int> SelectedTechnicianIds { get; set; } = new();

        public List<LabTechnician> AvailableTechnicians { get; set; }
            = new();
    }
}
