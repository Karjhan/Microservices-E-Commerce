using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Inventory.List;

public record ListInventoryQuery(InventoryFilter Filter) : IRequest<InventoryListDto>;
