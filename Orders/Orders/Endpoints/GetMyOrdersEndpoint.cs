using Application.Features.Orders.GetMyOrders;
using MediatR;

namespace Orders.Endpoints;

public static class GetMyOrdersEndpoint
{
    public static IEndpointRouteBuilder MapGetMyOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/user/{userId:guid}", async (
            Guid userId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyOrdersQuery(userId), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
