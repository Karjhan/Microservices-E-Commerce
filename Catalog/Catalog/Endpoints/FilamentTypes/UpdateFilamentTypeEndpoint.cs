using Application.Features.FilamentTypes.UpdateFilamentType;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.FilamentTypes;

public static class UpdateFilamentTypeEndpoint
{
    public static IEndpointRouteBuilder MapUpdateFilamentType(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateFilamentTypeRequest request, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(
                    new UpdateFilamentTypeCommand(id, request.Name, request.DiameterMm, request.ColorOptions), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record UpdateFilamentTypeRequest(string Name, decimal? DiameterMm = null, string[]? ColorOptions = null);
