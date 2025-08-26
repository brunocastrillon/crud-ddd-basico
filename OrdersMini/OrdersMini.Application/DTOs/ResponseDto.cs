namespace OrdersMini.Application.DTOs
{
    public record CustomerResponse(int Id, string Name, string Email, DateTime CreatedAt);
    public record ProductResponse(int Id, string Description, decimal Price, DateTime CreatedAt);
    public record OrderResponse(int Id, int CustomerId, DateTime CreatedAt, string Status, decimal Total, List<OrderItemResponse> Items);
    public record OrderItemResponse(int ProductId, string ProductDescription, int Quantity, decimal UnitPrice);
}