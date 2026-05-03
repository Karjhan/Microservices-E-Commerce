using Application.Features.Products.GetProductById;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class GetProductByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetProductById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProductByIdQuery(id));
            return Results.Ok(result);
        });

        return app;
    }
}