using Application.DTOs;
using Domain.Commons;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetProducts;

public record GetProductsQuery(ProductFilter Filter)
    : IRequest<List<ProductDto>>;