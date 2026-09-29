    namespace HemaCore.Models
{
    public class TechnicianTestType
    {
        public int TechnicianTestTypeId { get; set; }
        public int TestTypeId { get; set; }
        public TestType TestType { get; set; } //Test type navigation

        public int LabTechnicianId { get; set; }
        public LabTechnician LabTechnician { get; set; } //Lab technician navigation
    }
}
