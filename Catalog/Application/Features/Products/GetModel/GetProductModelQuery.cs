using Application.DTOs;
using MediatR;

namespace Application.Features.Products.GetModel;

public record GetProductModelQuery(Guid ProductId) : IRequest<ProductModelDto>;
