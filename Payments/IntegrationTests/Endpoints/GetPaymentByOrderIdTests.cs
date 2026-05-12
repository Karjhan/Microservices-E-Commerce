using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class GetPaymentByOrderIdTests : IntegrationTestBase
{
    public GetPaymentByOrderIdTests(PaymentsApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Return_Payment_By_OrderId()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        await Client.PostAsJsonAsync("/payments", new
        {
            OrderId = orderId,
            UserId = Guid.NewGuid(),
            Amount = 30m,
            Currency = "EUR",
            Method = 0,
            IdempotencyKey = Guid.NewGuid().ToString(),
            PaymentToken = "pm_test_token"
        });

        // Act
        var response = await Client.GetAsync($"/payments/by-order/{orderId}");

        // Assert
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(orderId.ToString());
    }

    [Fact]
    public async Task Should_Return_NotFound_For_Unknown_OrderId()
    {
        // Act
        var response = await Client.GetAsync($"/payments/by-order/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
