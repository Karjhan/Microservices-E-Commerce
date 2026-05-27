using Application.Features.Orders.GetOrderById;
using MediatR;

namespace Orders.Endpoints;

public static class GetOrderByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetOrderById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{orderId:guid}", async (
            Guid orderId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetOrderByIdQuery(orderId), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
