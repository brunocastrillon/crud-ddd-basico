namespace OrdersMini.Application.DTOs
{
    public record CustomerRequest(string Name, string Email);
    public record ProductRequest(string Description, decimal Price);
    public record OrderRequest(int CustomerId, List<OrderItemRequest> Items);
    public record OrderItemRequest(int ProductId, int Quantity);
}