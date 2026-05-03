using Domain.Commons;
using MediatR;

namespace Application.Features.Products.CreateProduct;

public record CreateProductCommand(
    string Name,
    string ShortDescription,
    string LongDescription,
    decimal Price,
    Guid CategoryId,

    string Currency,
    PrintSettings Settings,
    Dimensions Size,

    string[]? Tags,
    MaterialType[]? SupportedMaterials,
    PrinterType[]? CompatiblePrinters
) : IRequest<Guid>;