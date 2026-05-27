using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using RabbitMQ.Client;

namespace IntegrationTests.Endpoints;

public class PaymentConsumerTests : IntegrationTestBase
{
    public PaymentConsumerTests(OrdersApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Mark_Order_As_Paid_When_PaymentCompleted_Event_Received()
    {
        // Arrange — create an order
        var createRequest = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Gadget", UnitPrice = 100m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        var createResponse = await Client.PostAsJsonAsync("/orders", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Publish a payment.completed event via RabbitMQ
        var paymentCompletedEvent = new
        {
            PaymentId = Guid.NewGuid(),
            OrderId = created!.OrderId,
            UserId = createRequest.UserId,
            Amount = 100m,
            Currency = "EUR",
            ProviderPaymentId = "pi_test_123",
            CompletedAt = DateTime.UtcNow
        };

        await PublishToExchange("payments-exchange", "payment.completed", paymentCompletedEvent);

        // Wait for consumer to process
        await Task.Delay(2000);

        // Act
        var response = await Client.GetAsync($"/orders/{created.OrderId}");
        var order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>();

        // Assert
        order!.Status.Should().Be(Domain.Enums.OrderStatus.Paid);
    }

    [Fact]
    public async Task Should_Mark_Order_As_PaymentFailed_When_PaymentFailed_Event_Received()
    {
        // Arrange — create an order
        var createRequest = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Widget", UnitPrice = 50m, Quantity = 2, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        var createResponse = await Client.PostAsJsonAsync("/orders", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Publish a payment.failed event via RabbitMQ
        var paymentFailedEvent = new
        {
            PaymentId = Guid.NewGuid(),
            OrderId = created!.OrderId,
            UserId = createRequest.UserId,
            Amount = 100m,
            Currency = "EUR",
            Reason = "Insufficient funds",
            FailedAt = DateTime.UtcNow
        };

        await PublishToExchange("payments-exchange", "payment.failed", paymentFailedEvent);

        // Wait for consumer to process
        await Task.Delay(2000);

        // Act
        var response = await Client.GetAsync($"/orders/{created.OrderId}");
        var order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>();

        // Assert
        order!.Status.Should().Be(Domain.Enums.OrderStatus.PaymentFailed);
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
