using Application.Features.Products.UploadImage;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class UploadProductImageEndpoint
{
    public static IEndpointRouteBuilder MapUploadProductImage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/images", async (
                Guid id,
                IFormFile file,
                [FromForm] bool? isPrimary,
                IMediator mediator,
                CancellationToken ct) =>
            {
                // The handler checks size and detects the real type from the bytes (FR-015).
                await using var content = await OpenSeekableAsync(file, ct);

                var command = new UploadProductImageCommand(
                    id,
                    content,
                    file.Length,
                    file.FileName,
                    file.ContentType,
                    isPrimary ?? false);

                var image = await mediator.Send(command, ct);

                return Results.Created($"/products/{id}", image);
            })
            .DisableAntiforgery()
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }

    /// <summary>
    /// Form files are seekable. If one is not, it is copied only when it is small enough to be a valid
    /// image, so an oversized upload is refused without being buffered.
    /// </summary>
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
