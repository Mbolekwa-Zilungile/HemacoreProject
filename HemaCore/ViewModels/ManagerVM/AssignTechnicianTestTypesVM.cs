using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.ManagerVM
{
    public class AssignTechnicianTestTypesVM
    {
        [Required]
        public int LabTechnicianId { get; set; }

        public List<int> SelectedTestTypeIds { get; set; }
            = new List<int>();

        public List<SelectListItem> Technicians { get; set; }
            = new List<SelectListItem>();

        public List<SelectListItem> TestTypes { get; set; }
            = new List<SelectListItem>();
    }
}
