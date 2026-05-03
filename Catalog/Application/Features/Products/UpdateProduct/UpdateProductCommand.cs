using Domain.Commons;
using MediatR;

namespace Application.Features.Products.UpdateProduct;

public record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string ShortDescription,
    string LongDescription,
    decimal Price,

    string Currency,
    PrintSettings Settings,
    Dimensions Size,

    string[]? Tags,
    MaterialType[]? SupportedMaterials,
    PrinterType[]? CompatiblePrinters,
    
    int? DownloadCount,
    double? AverageRating
) : IRequest;