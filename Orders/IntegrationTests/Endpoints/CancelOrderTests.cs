using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class CancelOrderTests : IntegrationTestBase
{
    public CancelOrderTests(OrdersApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Cancel_Order()
    {
        // Arrange
        var createRequest = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new { Street = "St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Product", UnitPrice = 30m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        var createResponse = await Client.PostAsJsonAsync("/orders", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var cancelRequest = new { Reason = "Changed my mind" };

        // Act
        var response = await Client.PostAsJsonAsync($"/orders/{created!.OrderId}/cancel", cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await Client.GetAsync($"/orders/{created.OrderId}");
        var order = await getResponse.Content.ReadFromJsonAsync<OrderDetailResponse>();
        order!.Status.Should().Be(Domain.Enums.OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Order_Does_Not_Exist()
    {
        // Arrange
        var cancelRequest = new { Reason = "Reason" };

        // Act
        var response = await Client.PostAsJsonAsync($"/orders/{Guid.NewGuid()}/cancel", cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
