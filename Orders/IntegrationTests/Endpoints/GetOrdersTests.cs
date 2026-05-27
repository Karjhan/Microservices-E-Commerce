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
        // Arrange
        var request = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Thing", UnitPrice = 5m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        await Client.PostAsJsonAsync("/orders", request);

        // Act
        var response = await Client.GetAsync("/orders?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = await response.Content.ReadFromJsonAsync<List<OrderDetailResponse>>();
        orders.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Filter_By_UserId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var request = new
        {
            UserId = userId,
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Product", UnitPrice = 10m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        await Client.PostAsJsonAsync("/orders", request);

        // Act
        var response = await Client.GetAsync($"/orders?userId={userId}&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = await response.Content.ReadFromJsonAsync<List<OrderDetailResponse>>();
        orders.Should().NotBeEmpty();
        orders!.Should().AllSatisfy(o => o.UserId.Should().Be(userId));
    }
}
