using Application.DTOs;
using Domain.Commons;
using MediatR;

namespace Application.Features.Products.GetAdminProducts;

public record GetAdminProductsQuery(AdminProductFilter Filter) : IRequest<PaginatedListDto<ProductDto>>;
