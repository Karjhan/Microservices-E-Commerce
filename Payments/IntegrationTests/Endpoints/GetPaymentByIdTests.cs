using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class GetPaymentByIdTests : IntegrationTestBase
{
    public GetPaymentByIdTests(PaymentsApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Return_Payment()
    {
        // Arrange
        var paymentId = await CreatePayment();

        // Act
        var response = await Client.GetAsync($"/payments/{paymentId}");

        // Assert
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(paymentId.ToString());
    }

    [Fact]
    public async Task Should_Return_NotFound_For_Unknown_Id()
    {
        // Act
        var response = await Client.GetAsync($"/payments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreatePayment()
    {
        var res = await Client.PostAsJsonAsync("/payments", new
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 25m,
            Currency = "EUR",
            Method = 0,
            IdempotencyKey = Guid.NewGuid().ToString(),
            PaymentToken = "pm_test_token"
        });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PaymentResponse>();
        return body!.PaymentId;
    }
}
