using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestType
    {
        [Key]
        public int TestTypeId { get; set; }
        public string? TestTypeName { get; set; }
        public string? UnitOfMeasurement { get; set; }
        public double NormalRangeMin { get; set; }
        public double NormalRangeMax { get; set; }
        public int TurnAroundTime { get; set; }
        public bool IsActive { get; set; }

        public int CategoryId { get; set; }
        public TestCategory Category { get; set; } = null!;  //test category navigation property
        public int SampleTypeId { get; set; }

        public SampleType SampleType { get; set; } = null!;

        public ICollection<TechnicianTestType> TechnicianTestType { get; set; } = new List<TechnicianTestType>();
        public ICollection<TestTypeConsumable> TestTypeConsumables { get; set; }= new List<TestTypeConsumable>();

    }
}
