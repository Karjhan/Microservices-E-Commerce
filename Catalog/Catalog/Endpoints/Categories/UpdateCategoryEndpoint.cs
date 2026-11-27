using Application.Features.Categories.UpdateCategory;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Categories;

public static class UpdateCategoryEndpoint
{
    public static IEndpointRouteBuilder MapUpdateCategory(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new UpdateCategoryCommand(id, request.Name), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record UpdateCategoryRequest(string Name);
