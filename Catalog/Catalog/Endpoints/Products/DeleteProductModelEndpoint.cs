using Application.Features.Products.DeleteModel;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class DeleteProductModelEndpoint
{
    public static IEndpointRouteBuilder MapDeleteProductModel(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}/model", async (Guid id, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new DeleteProductModelCommand(id), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
