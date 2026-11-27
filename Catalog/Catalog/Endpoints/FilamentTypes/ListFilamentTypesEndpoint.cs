using Application.Features.FilamentTypes.ListFilamentTypes;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.FilamentTypes;

public static class ListFilamentTypesEndpoint
{
    public static IEndpointRouteBuilder MapListFilamentTypes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            bool? includeInactive,
            ICurrentUser user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Shoppers get active entries; only admins may see the inactive ones too.
            if (includeInactive == true)
            {
                if (!user.IsAuthenticated)
                    return Results.Unauthorized();

                if (!user.IsAdmin)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await mediator.Send(new ListFilamentTypesQuery(includeInactive == true), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
