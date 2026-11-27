using Application.Features.Categories.DeleteCategory;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Categories;

public static class DeleteCategoryEndpoint
{
    public static IEndpointRouteBuilder MapDeleteCategory(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
            {
                // 409 in_use when products still use it.
                await mediator.Send(new DeleteCategoryCommand(id), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
