using OrdersMini.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace OrdersMini.Infrastructure.Seed
{
    public static class AppDbSeeder
    {
        public static async Task SeedAsync(AppDbContext dbContext)
        {
            await dbContext.Database.MigrateAsync();

            if (!await dbContext.Customers.AnyAsync())
            {
                dbContext.Customers.AddRange(
                    new Customer("Bruno", "bruno@exemplo.com"),
                    new Customer("Vinicius", "vinicius@exemplo.com"),
                    new Customer("Heitor", "heitor@exemplo.com")
                );
            }

            if (!await dbContext.Products.AnyAsync())
            {
                dbContext.Products.AddRange(
                    Enumerable.Range(1, 30).Select(i => new Product($"product {i}", 10 + i))
                );
            }

            await dbContext.SaveChangesAsync();

            if (!await dbContext.Orders.AnyAsync())
            {
                Customer? customer = await dbContext.Customers.FirstOrDefaultAsync();

                Product? p1 = await dbContext.Products.FirstOrDefaultAsync();
                Product? p2 = await dbContext.Products.Skip(1).FirstOrDefaultAsync();

                Order? order = new() { CustomerId = customer.Id, Status = OrderStatus.Confirmed, Items = [] };

                order.Items.Add(new OrderItem { ProductId = p1.Id, Quantity = 3, UnitPrice = p1.Price });
                order.Items.Add(new OrderItem { ProductId = p2.Id, Quantity = 6, UnitPrice = p2.Price });

                order.Total = order.Items.Sum(x => x.UnitPrice * x.Quantity);

                dbContext.Orders.Add(order);

                dbContext.Orders.Add(new Order
                {
                    CustomerId = customer.Id,
                    Items = new List<OrderItem>
                    {
                        new()
                        {
                            ProductId = p2.Id,
                            Quantity = 9,
                            UnitPrice = p2.Price
                        }
                    },
                    Total = 3 * p2.Price
                });

                dbContext.Orders.Add(new Order
                {
                    CustomerId = customer.Id,
                    Items = new List<OrderItem>
                    {
                        new()
                        {
                            ProductId=p1.Id,
                            Quantity=3,
                            UnitPrice=p1.Price
                        }
                    },
                    Total = p1.Price
                });

                await dbContext.SaveChangesAsync();
            }
        }
    }
}