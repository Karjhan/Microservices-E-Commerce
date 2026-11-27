using Application.Features.FilamentTypes.DeleteFilamentType;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.FilamentTypes;

public static class DeleteFilamentTypeEndpoint
{
    public static IEndpointRouteBuilder MapDeleteFilamentType(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
            {
                // 409 in_use when products still use it.
                await mediator.Send(new DeleteFilamentTypeCommand(id), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
