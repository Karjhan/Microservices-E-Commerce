using Application.Features.Categories.ListCategories;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Categories;

public static class ListCategoriesEndpoint
{
    public static IEndpointRouteBuilder MapListCategories(this IEndpointRouteBuilder app)
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

            var result = await mediator.Send(new ListCategoriesQuery(includeInactive == true), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
