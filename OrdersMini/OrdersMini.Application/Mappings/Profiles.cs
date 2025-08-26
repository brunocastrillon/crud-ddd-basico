using AutoMapper;
using OrdersMini.Application.DTOs;
using OrdersMini.Domain.Entities;

namespace OrdersMini.Application.Mappings
{
    public class Profiles : Profile
    {
        public Profiles()
        {
            CreateMap<Customer, CustomerResponse>();
            CreateMap<Product, ProductResponse>();

            CreateMap<Order, OrderResponse>().ForMember(or => or.Status, m => m.MapFrom(o => o.Status.ToString()))
                                             .ForMember(or => or.Items, m => m.MapFrom(o => o.Items));

            CreateMap<OrderItem, OrderItemResponse>().ForMember(oir => oir.ProductId, m => m.MapFrom(oi => oi.ProductId))
                                                     .ForMember(oir => oir.ProductDescription, m => m.MapFrom(oi => oi.Product.Description));
        }
    }
}