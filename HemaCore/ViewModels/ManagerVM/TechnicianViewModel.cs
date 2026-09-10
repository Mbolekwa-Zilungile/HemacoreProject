namespace HemaCore.ViewModels.ManagerVM
{
    public class TechnicianViewModel
    {
        public int LabTechnicianId { get; set; }

        public string Name { get; set; }

        public string Surname { get; set; }

        public string IDNumber { get; set; }

        public string? EmployeeNumber { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public List<int> SelectedTestTypeIds { get; set; } = new();

        public List<TestTypeViewModel> TestTypes { get; set; } = new();
    }
}