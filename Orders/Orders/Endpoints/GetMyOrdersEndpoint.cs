using Application.Features.Orders.GetMyOrders;
using MediatR;
using Shared.Security.Authorization;

namespace Orders.Endpoints;

public static class GetMyOrdersEndpoint
{
    public static IEndpointRouteBuilder MapGetMyOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/user/{userId:guid}", async (
            Guid userId,
            ICurrentUser user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Owner rows: a caller may list only their own orders; anyone else gets 404 (admins may list any).
            if (!user.IsAdmin && user.Id != userId)
            {
                return Results.NotFound(new { error = "Orders not found", code = "order.not_found", status = 404 });
            }

            var result = await mediator.Send(new GetMyOrdersQuery(userId), ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        return app;
    }
}