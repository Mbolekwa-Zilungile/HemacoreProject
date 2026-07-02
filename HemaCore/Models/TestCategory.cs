using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestCategory
    {
        [Key]
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategoryDescription { get; set; }
        public bool IsActive { get; set; }
        //TestType relationship
        public ICollection<TestType> TestTypes { get; set; }
    }
}
