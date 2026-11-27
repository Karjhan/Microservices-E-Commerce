using Application.Features.Brands.SetBrandActive;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Brands;

public static class SetBrandActiveEndpoint
{
    public static IEndpointRouteBuilder MapSetBrandActive(this IEndpointRouteBuilder app)
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
        await mediator.Send(new SetBrandActiveCommand(id, active), ct);

        return Results.NoContent();
    }
}
