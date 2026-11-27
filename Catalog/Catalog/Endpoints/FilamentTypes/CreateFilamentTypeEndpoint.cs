using Application.Features.FilamentTypes.CreateFilamentType;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.FilamentTypes;

public static class CreateFilamentTypeEndpoint
{
    public static IEndpointRouteBuilder MapCreateFilamentType(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (CreateFilamentTypeRequest request, IMediator mediator, CancellationToken ct) =>
            {
                var id = await mediator.Send(
                    new CreateFilamentTypeCommand(request.Name, request.Code, request.DiameterMm, request.ColorOptions), ct);

                return Results.Created($"/filament-types/{id}", new { id });
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record CreateFilamentTypeRequest(string Name, string? Code = null, decimal? DiameterMm = null, string[]? ColorOptions = null);
