
namespace HemaCore.Models
{
    public class ConsentAllergy
    {
        public int Id { get; set; }

        public int ConsentId { get; set; }

        public int AllergyId { get; set; }

        public Consent Consent { get; set; } = null!;

        public Allergy Allergy { get; set; } = null!;
    }
}
