using OrdersMini.Domain.Validations;

namespace OrdersMini.Domain.Entities
{
    public sealed class Product : BaseEntity
    {
        public string? Description { get; private set; }
        public decimal Price { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        public Product(string description, decimal price)
        {
            ValidateDomain(description, price);
        }

        public Product(int id, string description, decimal price)
        {
            ValidateId(id);
            ValidateDomain(description, price);
        }

        public Product()
        {
            
        }

        private void ValidateDomain(string description, decimal price)
        {
            DomainExceptionValidation.When(string.IsNullOrEmpty(description), "description is required");
            DomainExceptionValidation.When(price < 0, "invalid price");

            Description = description;
            Price = price;  
        }
    }
}
