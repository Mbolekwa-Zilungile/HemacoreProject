namespace HemaCore.Models
{
    public class TestTypeConsumable
    {
        public int TestTypeId { get; set; }
        public TestType TestType { get; set; }

        public int ConsumableId { get; set; }
        public Consumable Consumable { get; set; } 
    }
}
