using OrdersMini.Domain.Validations;

namespace OrdersMini.Domain.Entities
{
    public sealed class Customer : BaseEntity
    {
        public string? Name { get; private set; }
        public string? Email { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public bool IsDeleted { get; private set; } = false;

        public Customer(string name, string email)
        {
            ValidationDomain(name, email);
        }

        public Customer(int id, string name, string email)
        {
            ValidateId(id);
            ValidationDomain(name, email);
        }

        public Customer()
        {
                
        }

        public void SetDelete()
        {
            IsDeleted = !IsDeleted;
        }

        private void ValidationDomain(string name, string email)
        {
            DomainExceptionValidation.When(string.IsNullOrEmpty(name), "name is required");
            DomainExceptionValidation.When(string.IsNullOrEmpty(email), "email is required");

            Name = name;
            Email = email;
        }
    }
}