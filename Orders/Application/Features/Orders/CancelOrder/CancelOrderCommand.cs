using MediatR;

namespace Application.Features.Orders.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest;
