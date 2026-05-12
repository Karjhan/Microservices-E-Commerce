using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class GetPaymentsTests : IntegrationTestBase
{
    public GetPaymentsTests(PaymentsApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Return_Payments_List()
    {
        // Arrange
        await Client.PostAsJsonAsync("/payments", new
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 10m,
            Currency = "EUR",
            Method = 0,
            IdempotencyKey = Guid.NewGuid().ToString(),
            PaymentToken = "pm_test_token"
        });

        // Act
        var response = await Client.GetAsync("/payments?page=1&pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Filter_By_Status()
    {
        // Arrange
        await Client.PostAsJsonAsync("/payments", new
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 20m,
            Currency = "EUR",
            Method = 0,
            IdempotencyKey = Guid.NewGuid().ToString(),
            PaymentToken = "pm_test_token"
        });

        // Act - filter for Completed (status=2), should return empty since new payments are Pending
        var response = await Client.GetAsync("/payments?status=2&page=1&pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();

        var list = await response.Content.ReadFromJsonAsync<List<object>>();
        list.Should().BeEmpty();
    }
}
