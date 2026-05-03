using Application.DTOs;
using MediatR;

namespace Application.Features.Products.GetProductById;

public record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDto>;