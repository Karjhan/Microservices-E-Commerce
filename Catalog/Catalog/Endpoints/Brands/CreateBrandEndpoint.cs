using Application.Features.Brands.CreateBrand;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Brands;

public static class CreateBrandEndpoint
{
    public static IEndpointRouteBuilder MapCreateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (CreateBrandRequest request, IMediator mediator, CancellationToken ct) =>
            {
                var id = await mediator.Send(new CreateBrandCommand(request.Name), ct);

                return Results.Created($"/brands/{id}", new { id });
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record CreateBrandRequest(string Name);
