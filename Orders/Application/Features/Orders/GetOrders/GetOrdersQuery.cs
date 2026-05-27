using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Orders.GetOrders;

public record GetOrdersQuery(OrderFilter Filter) : IRequest<List<OrderDto>>;
