using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class GetOrdersTests : IntegrationTestBase
{
    public GetOrdersTests(OrdersApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Return_Orders_List()
    {
        var request = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Thing", UnitPrice = 5m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        await Client.PostAsJsonAsync("/orders", request);

        var response = await Client.GetAsync("/orders?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedListResponse<OrderDetailResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Filter_By_UserId()
    {
        var userId = Guid.NewGuid();

        var request = new
        {
            UserId = userId,
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Product", UnitPrice = 10m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        await Client.PostAsJsonAsync("/orders", request);

        var response = await Client.GetAsync($"/orders?userId={userId}&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedListResponse<OrderDetailResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(o => o.UserId.Should().Be(userId));
    }
}