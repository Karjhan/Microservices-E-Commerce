using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class CreatePaymentTests : IntegrationTestBase
{
    public CreatePaymentTests(PaymentsApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Create_Payment()
    {
        // Arrange
        var request = new
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 99.99m,
            Currency = "EUR",
            Method = 0, // CreditCard
            IdempotencyKey = Guid.NewGuid().ToString(),
            PaymentToken = "pm_test_token"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/payments", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        body!.PaymentId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Reject_Duplicate_IdempotencyKey()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();

        var request = new
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 50m,
            Currency = "EUR",
            Method = 0,
            IdempotencyKey = idempotencyKey,
            PaymentToken = "pm_test_token"
        };

        // Act
        var first = await Client.PostAsJsonAsync("/payments", request);
        first.EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/payments", new
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 75m,
            Currency = "USD",
            Method = 1,
            IdempotencyKey = idempotencyKey,
            PaymentToken = "pm_other_token"
        });

        // Assert
        second.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }
}
