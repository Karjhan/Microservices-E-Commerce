using Application.DTOs;
using MediatR;

namespace Application.Features.Products.UploadImage;

public record UploadProductImageCommand(
    Guid ProductId,
    string FilePath,
    string FileName,
    string ContentType,
    bool IsPrimary
) : IRequest<UploadProductImageDto>;