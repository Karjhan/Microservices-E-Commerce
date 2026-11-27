using Application.DTOs;
using MediatR;

namespace Application.Features.Categories.ListCategories;

/// <summary>Active entries only, unless an admin asks for the inactive ones too.</summary>
public record ListCategoriesQuery(bool IncludeInactive) : IRequest<List<CategoryDto>>;
