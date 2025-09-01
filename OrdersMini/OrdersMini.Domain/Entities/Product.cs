using OrdersMini.Domain.Validations;

namespace OrdersMini.Domain.Entities
{
    public sealed class Product : BaseEntity
    {
        public string Description { get; private set; } = default!;
        public decimal Price { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// construtor vazio para EF
        /// </summary>
        public Product() { }

        /// <summary>
        /// construtor para create
        /// </summary>
        /// <param name="description"></param>
        /// <param name="price"></param>
        public Product(string description, decimal price)
        {
            ValidateDomain(description, price);
        }

        public void Update(string description, decimal price) => ValidateDomain(description, price);

        public void SetDelete() => IsDeleted = !IsDeleted;

        private void ValidateDomain(string description, decimal price)
        {
            DomainExceptionValidation.When(string.IsNullOrEmpty(description), "description is required");
            DomainExceptionValidation.When(price < 0, "invalid price");

            Description = description;
            Price = price;  
        }
    }
}