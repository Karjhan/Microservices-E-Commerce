using Domain.Commons;
using MediatR;
using Shared.Security.Audit;

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
) : IRequest<Guid>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.create";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => null; // the new product id is taken from the response
    object? IAuditedRequest.AuditDetails => new { name = Name, price = Price, currency = Currency };
}