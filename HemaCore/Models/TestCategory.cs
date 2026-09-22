using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class TestCategory
    {
        [Key]
        public int CategoryId { get; set; }

        [Required(ErrorMessage ="Test Category name is required.")]
        public string CategoryName { get; set; }

        [Required(ErrorMessage = "Test Category description is required.")]
        public string CategoryDescription { get; set; }
        public bool IsActive { get; set; }
        //TestType relationship
        public ICollection<TestType> TestTypes { get; set; } = new List<TestType>();
    }
}
