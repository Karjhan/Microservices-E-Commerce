using Application.Features.Categories.SetCategoryActive;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Categories;

public static class SetCategoryActiveEndpoint
{
    public static IEndpointRouteBuilder MapSetCategoryActive(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/activate", (Guid id, IMediator mediator, CancellationToken ct) =>
                Send(id, true, mediator, ct))
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        app.MapPost("/{id:guid}/deactivate", (Guid id, IMediator mediator, CancellationToken ct) =>
                Send(id, false, mediator, ct))
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }

    private static async Task<IResult> Send(Guid id, bool active, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new SetCategoryActiveCommand(id, active), ct);

        return Results.NoContent();
    }
}
