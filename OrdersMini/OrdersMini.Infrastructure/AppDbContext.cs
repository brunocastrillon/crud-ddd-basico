using Microsoft.EntityFrameworkCore;
using OrdersMini.Domain.Entities;

namespace OrdersMini.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();

            modelBuilder.Entity<Product>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            
            modelBuilder.Entity<Order>().Property(o => o.Total).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Order>().HasIndex(o => new { o.CustomerId, o.CreatedAt });
            modelBuilder.Entity<Order>().HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>().Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderItem>().HasOne(oi => oi.Product).WithMany().HasForeignKey(oi => oi.ProductId);

            base.OnModelCreating(modelBuilder);
        }
    }
}