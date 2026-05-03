using MediatR;

namespace Application.Features.Products.DeleteImage;

public record DeleteProductImageCommand(Guid ProductId, Guid ImageId) : IRequest;