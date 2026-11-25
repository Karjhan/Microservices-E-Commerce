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
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteProductCommand(id));
            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}