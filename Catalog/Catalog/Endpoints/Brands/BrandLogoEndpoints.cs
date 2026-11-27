using Application.Features.Brands.RemoveLogo;
using Application.Features.Brands.UploadLogo;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Brands;

public static class BrandLogoEndpoints
{
    public static IEndpointRouteBuilder MapBrandLogo(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}/logo", async (Guid id, IFormFile file, IMediator mediator, CancellationToken ct) =>
            {
                // The handler checks size and detects the real type from the bytes, as for product images (FR-015).
                await using var content = await OpenSeekableAsync(file, ct);

                var url = await mediator.Send(
                    new UploadBrandLogoCommand(id, content, file.Length, file.FileName, file.ContentType), ct);

                return Results.Ok(new { logoUrl = url });
            })
            .DisableAntiforgery()
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        app.MapDelete("/{id:guid}/logo", async (Guid id, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new RemoveBrandLogoCommand(id), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }

    /// <summary>Same as the product image upload: oversized streams are refused without being buffered.</summary>
    private static async Task<Stream> OpenSeekableAsync(IFormFile file, CancellationToken ct)
    {
        var stream = file.OpenReadStream();

        if (stream.CanSeek || file.Length > Domain.Entities.ProductImage.MaxSizeBytes)
            return stream;

        var copy = new MemoryStream();
        await using (stream)
            await stream.CopyToAsync(copy, ct);

        copy.Position = 0;
        return copy;
    }
}
