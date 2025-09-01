using OrdersMini.Domain.Validations;

namespace OrdersMini.Domain.Entities
{
    public abstract class BaseEntity
    {
        public int Id { get; protected set; }

        public void ValidateId(int id)
        {
            DomainExceptionValidation.When(id < 0, "invalid id");

            Id = id;
        }
    }
}