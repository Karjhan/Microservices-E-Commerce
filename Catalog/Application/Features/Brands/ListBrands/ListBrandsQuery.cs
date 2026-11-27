using Application.DTOs;
using MediatR;

namespace Application.Features.Brands.ListBrands;

/// <summary>Active entries only, unless an admin asks for the inactive ones too.</summary>
public record ListBrandsQuery(bool IncludeInactive) : IRequest<List<BrandDto>>;
