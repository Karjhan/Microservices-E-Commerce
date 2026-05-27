using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class GetOrderByIdTests : IntegrationTestBase
{
    public GetOrderByIdTests(OrdersApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Return_Order_By_Id()
    {
        // Arrange
        var createRequest = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new
            {
                Street = "Test St",
                City = "City",
                State = "State",
                Country = "Country",
                ZipCode = "00000"
            },
            Items = new[]
            {
                new { ProductId = Guid.NewGuid(), ProductName = "Widget", UnitPrice = 15m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/orders", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await Client.GetAsync($"/orders/{created!.OrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailResponse>();
        order!.Id.Should().Be(created.OrderId);
        order.Currency.Should().Be("EUR");
        order.Items.Should().HaveCount(1);
        order.ShippingAddress.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Order_Does_Not_Exist()
    {
        // Act
        var response = await Client.GetAsync($"/orders/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
