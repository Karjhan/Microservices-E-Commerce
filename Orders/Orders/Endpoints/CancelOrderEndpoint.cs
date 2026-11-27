using Application.Features.Orders.CancelOrder;
using MediatR;

namespace Orders.Endpoints;

public static class CancelOrderEndpoint
{
    public static IEndpointRouteBuilder MapCancelOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{orderId:guid}/cancel", async (
            Guid orderId,
            CancelOrderRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new CancelOrderCommand(orderId, request.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}

public record CancelOrderRequest(string Reason);
