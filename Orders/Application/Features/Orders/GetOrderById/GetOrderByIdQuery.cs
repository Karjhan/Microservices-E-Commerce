using Application.DTOs;
using MediatR;

namespace Application.Features.Orders.GetOrderById;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDto>;
