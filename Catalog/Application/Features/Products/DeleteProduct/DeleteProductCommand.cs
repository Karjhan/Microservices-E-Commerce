using MediatR;

namespace Application.Features.Products.DeleteProduct;

public record DeleteProductCommand(Guid ProductId) : IRequest;