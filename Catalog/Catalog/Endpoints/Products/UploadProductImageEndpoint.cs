using Application.Features.Products.UploadImage;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Endpoints.Products;

public static class UploadProductImageEndpoint
{
    public static IEndpointRouteBuilder MapUploadProductImage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/images", async (
                Guid id,
                IFormFile file,
                [FromForm] bool isPrimary,
                IMediator mediator,
                CancellationToken ct) =>
            {
                if (file.Length > 5 * 1024 * 1024)
                    return Results.BadRequest("File too large");

                var tempFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}");

                try
                {
                    await using (var input = file.OpenReadStream())
                    await using (var output = File.Create(tempFilePath))
                    {
                        await input.CopyToAsync(output, ct);
                    }

                    var command = new UploadProductImageCommand(
                        id,
                        tempFilePath,
                        file.FileName,
                        file.ContentType,
                        isPrimary
                    );

                    var result = await mediator.Send(command, ct);

                    return Results.Ok(result);
                }
                finally
                {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
            })
            .DisableAntiforgery();

        return app;
    }
}