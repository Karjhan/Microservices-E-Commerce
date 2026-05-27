using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Orders.GetOrderById;

public class GetOrderByIdHandler(IOrderRepository repo, ICacheService cache)
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"order:{request.OrderId}";

        var cached = await cache.GetAsync<OrderDto>(cacheKey);
        if (cached is not null)
            return cached;

        var order = await repo.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        var dto = OrderDto.FromEntity(order);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
