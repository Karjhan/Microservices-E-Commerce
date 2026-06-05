using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using RabbitMQ.Client;

namespace IntegrationTests.Flows;

public class StockLifecycleFlowTests : IntegrationTestBase
{
    public StockLifecycleFlowTests(InventoryApiFactory factory) : base(factory) { }

    [Fact]
    public async Task PaymentCompleted_Should_Deduct_Reserved_Stock()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        (await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = 10, reason = "test seed" })).EnsureSuccessStatusCode();

        await PublishToExchange("orders-exchange", "order.created", new
        {
            orderId,
            userId = Guid.NewGuid(),
            totalAmount = 100m,
            currency = "EUR",
            itemCount = 1,
            createdAt = DateTime.UtcNow,
            items = new[] { new { productId, quantity = 4 } }
        });

        await WaitForAsync(productId, dto => dto.ReservedQuantity == 4);

        await PublishToExchange("payments-exchange", "payment.completed", new
        {
            paymentId = Guid.NewGuid(),
            orderId,
            userId = Guid.NewGuid(),
            amount = 100m,
            currency = "EUR",
            providerPaymentId = "pi_test_123",
            completedAt = DateTime.UtcNow
        });

        await WaitForAsync(productId, dto => dto.OnHandQuantity == 6 && dto.ReservedQuantity == 0);
    }

    [Fact]
    public async Task PaymentFailed_Should_Release_Reservation()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        (await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = 10, reason = "test seed" })).EnsureSuccessStatusCode();

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

        await WaitForAsync(productId, dto => dto.ReservedQuantity == 5);

        await PublishToExchange("payments-exchange", "payment.failed", new
        {
            paymentId = Guid.NewGuid(),
            orderId,
            userId = Guid.NewGuid(),
            amount = 100m,
            currency = "EUR",
            reason = "Insufficient funds",
            failedAt = DateTime.UtcNow
        });

        await WaitForAsync(productId, dto => dto.ReservedQuantity == 0 && dto.OnHandQuantity == 10);
    }

    [Fact]
    public async Task OrderCancelled_Should_Release_Reservation()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        (await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = 8, reason = "test seed" })).EnsureSuccessStatusCode();

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

        await WaitForAsync(productId, dto => dto.ReservedQuantity == 3);

        await PublishToExchange("orders-exchange", "order.cancelled", new
        {
            orderId,
            userId = Guid.NewGuid(),
            reason = "Customer cancelled",
            cancelledAt = DateTime.UtcNow
        });

        await WaitForAsync(productId, dto => dto.ReservedQuantity == 0 && dto.OnHandQuantity == 8);
    }

    private async Task WaitForAsync(Guid productId, Func<InventoryItemResponse, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            var response = await Client.GetAsync($"/inventory/{productId}");
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<InventoryItemResponse>();
                if (dto is not null && predicate(dto))
                    return;
            }

            await Task.Delay(250);
        }

        Assert.Fail($"Inventory item {productId} did not reach expected state within timeout.");
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
