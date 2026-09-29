using System.ComponentModel.DataAnnotations;

namespace HemaCore.ViewModels.ManagerVM
{
    public class TestTypeViewModel
    {
        public int TestTypeId { get; set; }

        [Required(ErrorMessage = "Test Type Name is required")]
        public string? TestTypeName { get; set; }

        [Required(ErrorMessage = "Unit of Measurement is required")]
        public string? UnitOfMeasurement { get; set; }

        [Required(ErrorMessage = "Minimum normal range is required")]
        [Range(0, double.MaxValue,
            ErrorMessage = "Minimum range cannot be negative")]
        public double NormalRangeMin { get; set; }

        [Required(ErrorMessage = "Maximum normal range is required")]
        [Range(0, double.MaxValue,
            ErrorMessage = "Maximum range cannot be negative")]
        public double NormalRangeMax { get; set; }

        [Required(ErrorMessage = "Turn Around Time is required")]
        [Range(1, int.MaxValue,
            ErrorMessage = "Turn Around Time must be greater than 0")]
        public int TurnAroundTime { get; set; }

        [Required(ErrorMessage = "Please select a category")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Please select a sample type")]
        public int SampleTypeID { get; set; }

        public bool IsActive { get; set; } = true;

        // Consumables assigned to this test type
        public List<ConsumableVM> Consumables { get; set; }
            = new List<ConsumableVM>();

        // Technicians assigned to this test type
        public List<TechnicianVM> Technicians { get; set; }
            = new List<TechnicianVM>();
    }
}
