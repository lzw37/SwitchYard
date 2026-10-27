namespace SwitchYard.Capacity
{
    public class CapacityInstance
    {
        public string ID { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Owner { get; set; } = string.Empty;

        public DateTime? CreatedDate { get; set; }

        public int IsActive { get; set; }
    }
}
