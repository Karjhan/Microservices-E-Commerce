using Application.Features.FilamentTypes.SetFilamentTypeActive;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.FilamentTypes;

public static class SetFilamentTypeActiveEndpoint
{
    public static IEndpointRouteBuilder MapSetFilamentTypeActive(this IEndpointRouteBuilder app)
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
        await mediator.Send(new SetFilamentTypeActiveCommand(id, active), ct);

        return Results.NoContent();
    }
}
