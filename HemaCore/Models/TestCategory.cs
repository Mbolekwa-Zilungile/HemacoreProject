using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestCategory
    {
        [Key]
        public int CategoryID { get; set; }

        public string CategoryName { get; set; }
        public string CategoryDescription { get; set; }

        //public List<TestType> TestTypes { get; set; } = new List<TestType>();
    }
}
