using Application.Features.Products.DeleteProduct;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class DeleteProductEndpoint
{
    public static IEndpointRouteBuilder MapDeleteProduct(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            IMediator mediator) =>
        {
            // Deleting is irreversible: the caller must send confirm=true (FR-018).
            await mediator.Send(new DeleteProductCommand(id, confirm ?? false));
            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}