using Application.Features.Categories.CreateCategory;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Categories;

public static class CreateCategoryEndpoint
{
    public static IEndpointRouteBuilder MapCreateCategory(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (CreateCategoryRequest request, IMediator mediator, CancellationToken ct) =>
            {
                var id = await mediator.Send(new CreateCategoryCommand(request.Name), ct);

                return Results.Created($"/categories/{id}", new { id });
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record CreateCategoryRequest(string Name);
