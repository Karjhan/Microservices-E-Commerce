using Application.Features.Products.UploadModel;
using Catalog.Middleware;
using Domain.Entities;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class UploadProductModelEndpoint
{
    /// <summary>Body limit for this route only: the 100 MB model plus multipart overhead (research R8).</summary>
    public const long RequestBodyLimitBytes = ProductModel.MaxSizeBytes + 1024 * 1024;

    public static IEndpointRouteBuilder MapUploadProductModel(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}/model", async (
                Guid id,
                IFormFile file,
                IMediator mediator,
                CancellationToken ct) =>
            {
                await using var content = file.OpenReadStream();

                var result = await mediator.Send(
                    new UploadProductModelCommand(id, content, file.Length, file.FileName), ct);

                return result.Created
                    ? Results.Created($"/products/{id}/model", result.Model)
                    : Results.Ok(result.Model);
            })
            .DisableAntiforgery()
            // Raised for this endpoint only; every other route keeps the server default.
            .WithMetadata(new MaxRequestBodySizeMetadata(RequestBodyLimitBytes))
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
