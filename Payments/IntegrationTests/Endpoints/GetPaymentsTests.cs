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

        var response = await Client.GetAsync("/payments?page=1&pageSize=10");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedListResponse<PaymentResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Filter_By_Status()
    {
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

        var response = await Client.GetAsync("/payments?status=2&page=1&pageSize=10");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedListResponse<PaymentResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}