using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Enrichment;
using Domain.Exceptions;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Enrichment;

public class OrdersHttpClient : IOrderEnrichmentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrdersHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrdersHttpClient(HttpClient httpClient, ILogger<OrdersHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderEnrichmentDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"/orders/{orderId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                throw new OrderEnrichmentException(
                    $"Order enrichment failed: HTTP {(int)response.StatusCode}",
                    isTransient: (int)response.StatusCode >= 500);
            }

            var order = await response.Content.ReadFromJsonAsync<OrdersApiOrderDto>(JsonOptions, cancellationToken);
            return order?.ToEnrichmentDto();
        }
        catch (OrderEnrichmentException)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Order enrichment timed out for order {OrderId}", orderId);
            throw new OrderEnrichmentException("Order enrichment timed out", isTransient: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order enrichment failed for order {OrderId}", orderId);
            throw new OrderEnrichmentException(ex.Message, isTransient: true);
        }
    }

    private sealed class OrdersApiOrderDto
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public decimal TotalAmount { get; init; }
        public string Currency { get; init; } = default!;
        public ShippingAddressApiDto? ShippingAddress { get; init; }
        public List<OrderItemApiDto> Items { get; init; } = [];

        public OrderEnrichmentDto ToEnrichmentDto() => new()
        {
            Id = Id,
            UserId = UserId,
            TotalAmount = TotalAmount,
            Currency = Currency,
            ShippingAddress = ShippingAddress is null ? null : new ShippingAddressEnrichmentDto
            {
                Street = ShippingAddress.Street,
                City = ShippingAddress.City,
                State = ShippingAddress.State,
                Country = ShippingAddress.Country,
                ZipCode = ShippingAddress.ZipCode,
                FullName = ShippingAddress.FullName
            },
            Items = Items.Select(i => new OrderItemEnrichmentDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Currency = i.Currency,
                LineTotal = i.LineTotal
            }).ToList()
        };
    }

    private sealed class OrderItemApiDto
    {
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = default!;
        public decimal UnitPrice { get; init; }
        public int Quantity { get; init; }
        public string Currency { get; init; } = default!;
        public decimal LineTotal { get; init; }
    }

    private sealed class ShippingAddressApiDto
    {
        public string Street { get; init; } = default!;
        public string City { get; init; } = default!;
        public string? State { get; init; }
        public string Country { get; init; } = default!;
        public string ZipCode { get; init; } = default!;
        public string? FullName { get; init; }
    }
}
