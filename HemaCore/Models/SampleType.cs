using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class SampleType
    {
        [Key]
        public int SampleTypeId { get; set; }
        public string SampleName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }

        public ICollection<TestType> TestTypes { get; set; } = new List<TestType>();
        public ICollection<Sample> Samples{ get; set; } =  new List<Sample>(); // Sample navigation
    }
}
