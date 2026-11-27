using Domain.Commons;
using MediatR;
using Shared.Security.Audit;

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
    PrinterType[]? CompatiblePrinters,

    // Null keeps the current category.
    Guid? CategoryId = null,

    // The brand is replaced as sent: null removes it.
    Guid? BrandId = null,

    // Null keeps the current filament types; a list replaces them.
    Guid[]? FilamentTypeIds = null
) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.update";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { name = Name, price = Price, currency = Currency };
}