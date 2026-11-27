using Application.Abstractions.Persistence;
using Application.Features.Orders.GetOrders;
using Domain.Enums;
using MediatR;
using Shared.Security.Authorization;

namespace Orders.Endpoints;

public static class GetOrdersEndpoint
{
    public static IEndpointRouteBuilder MapGetOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            Guid? userId,
            OrderStatus? status,
            DateTime? fromDate,
            DateTime? toDate,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var filter = new OrderFilter
            {
                UserId = userId,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page ?? 1,
                PageSize = pageSize ?? 20
            };

            var result = await mediator.Send(new GetOrdersQuery(filter), ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
