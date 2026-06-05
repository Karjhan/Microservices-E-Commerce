using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using RabbitMQ.Client;

namespace IntegrationTests.Flows;

public class ReserveStockFlowTests : IntegrationTestBase
{
    public ReserveStockFlowTests(InventoryApiFactory factory) : base(factory) { }

    [Fact]
    public async Task OrderCreated_With_In_Stock_Lines_Should_Reserve_Stock()
    {
        var productId = Guid.NewGuid();

        var seedResponse = await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = 10, reason = "integration test seed" });
        seedResponse.EnsureSuccessStatusCode();

        var orderId = Guid.NewGuid();

        await PublishToExchange("orders-exchange", "order.created", new
        {
            orderId,
            userId = Guid.NewGuid(),
            totalAmount = 100m,
            currency = "EUR",
            itemCount = 1,
            createdAt = DateTime.UtcNow,
            items = new[] { new { productId, quantity = 3 } }
        });

        await WaitForReservedAsync(productId, expected: 3);

        var getResponse = await Client.GetAsync($"/inventory/{productId}");
        getResponse.EnsureSuccessStatusCode();

        var dto = await getResponse.Content.ReadFromJsonAsync<InventoryItemResponse>();

        dto!.ProductId.Should().Be(productId);
        dto.OnHandQuantity.Should().Be(10);
        dto.ReservedQuantity.Should().Be(3);
        dto.AvailableQuantity.Should().Be(7);
    }

    [Fact]
    public async Task OrderCreated_With_Insufficient_Stock_Should_Not_Reserve()
    {
        var productId = Guid.NewGuid();

        var seedResponse = await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = 2, reason = "integration test seed" });
        seedResponse.EnsureSuccessStatusCode();

        var orderId = Guid.NewGuid();

        await PublishToExchange("orders-exchange", "order.created", new
        {
            orderId,
            userId = Guid.NewGuid(),
            totalAmount = 100m,
            currency = "EUR",
            itemCount = 1,
            createdAt = DateTime.UtcNow,
            items = new[] { new { productId, quantity = 5 } }
        });

        await Task.Delay(2000);

        var getResponse = await Client.GetAsync($"/inventory/{productId}");
        getResponse.EnsureSuccessStatusCode();

        var dto = await getResponse.Content.ReadFromJsonAsync<InventoryItemResponse>();

        dto!.ReservedQuantity.Should().Be(0);
        dto.AvailableQuantity.Should().Be(2);
    }

    private async Task WaitForReservedAsync(Guid productId, int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            var response = await Client.GetAsync($"/inventory/{productId}");
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<InventoryItemResponse>();
                if (dto!.ReservedQuantity == expected)
                    return;
            }

            await Task.Delay(250);
        }
    }

    private async Task PublishToExchange(string exchange, string routingKey, object message)
    {
        var factory = new ConnectionFactory
        {
            HostName = Factory.RabbitHost,
            Port = Factory.RabbitPort,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: true);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            body: body);
    }
}

public class InventoryItemResponse
{
    public Guid ProductId { get; set; }
    public int OnHandQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsDepleted { get; set; }
    public DateTime UpdatedAt { get; set; }
}
