using FluentValidation;
using OrdersMini.Application.DTOs;

namespace OrdersMini.Application.Validations
{
    public class CustomerValidation : AbstractValidator<CustomerRequest>
    {
        public CustomerValidation()
        {
            RuleFor(cr => cr.Name).NotEmpty().MaximumLength(120);
            RuleFor(cr => cr.Email).NotEmpty().EmailAddress();
        }
    }

    public class ProductValidation : AbstractValidator<ProductRequest>
    {
        public ProductValidation()
        {
            RuleFor(pr => pr.Description).NotEmpty().MaximumLength(120);
            RuleFor(pr => pr.Price).GreaterThan(0);
        }
    }

    public class OrderValidation : AbstractValidator<OrderRequest>
    {
        public OrderValidation()
        {
            RuleFor(or => or.CustomerId).GreaterThan(0);
            RuleFor(or => or.Items).NotEmpty();

            RuleForEach(x => x.Items).ChildRules(y =>
            {
                y.RuleFor(z => z.ProductId).GreaterThan(0);
                y.RuleFor(z => z.Quantity).GreaterThan(0);
            });
        }
    }
}