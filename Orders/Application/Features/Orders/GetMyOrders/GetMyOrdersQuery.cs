using Application.DTOs;
using MediatR;

namespace Application.Features.Orders.GetMyOrders;

public record GetMyOrdersQuery(Guid UserId) : IRequest<List<OrderDto>>;
