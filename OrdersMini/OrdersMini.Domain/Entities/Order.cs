namespace OrdersMini.Domain.Entities
{
    public sealed class Order : BaseEntity
    {
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        public List<OrderItem> Items { get; set; }

        public Order()
        {
                
        }
    }

    public enum OrderStatus { Pending = 0, Confirmed = 1, Cancelled = 2 }
}
