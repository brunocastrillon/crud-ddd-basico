using OrdersMini.Domain.Validations;

namespace OrdersMini.Domain.Entities
{
    public sealed class Customer : BaseEntity
    {
        public string Name { get; private set; } = default!;
        public string Email { get; private set; } = default!;
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// construtor vazio para EF
        /// </summary>
        public Customer() { }

        /// <summary>
        /// construtor para create
        /// </summary>
        /// <param name="name"></param>
        /// <param name="email"></param>
        public Customer(string name, string email)
        {
            ValidationDomain(name, email);
        }

        public void Update(string name, string email) => ValidationDomain(name, email);

        public void SetDelete() => IsDeleted = !IsDeleted;

        private void ValidationDomain(string name, string email)
        {
            DomainExceptionValidation.When(string.IsNullOrEmpty(name), "name is required");
            DomainExceptionValidation.When(string.IsNullOrEmpty(email), "email is required");

            Name = name.Trim();
            Email = email.Trim().ToLowerInvariant();
        }
    }
}