using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class UpdateShippingAddressTests : IntegrationTestBase
{
    public UpdateShippingAddressTests(OrdersApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Update_Shipping_Address()
    {
        // Arrange
        var createRequest = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new { Street = "Old St", City = "OldCity", State = "OldState", Country = "OldCountry", ZipCode = "00000" },
            Items = new[] { new { ProductId = Guid.NewGuid(), ProductName = "Product", UnitPrice = 20m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null } }
        };

        var createResponse = await Client.PostAsJsonAsync("/orders", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var updateRequest = new
        {
            Street = "New Street 456",
            City = "NewCity",
            State = "NewState",
            Country = "NewCountry",
            ZipCode = "99999",
            FullName = "Jane Doe",
            Phone = "+1234567890"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/orders/{created!.OrderId}/shipping-address", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify address changed
        var getResponse = await Client.GetAsync($"/orders/{created.OrderId}");
        var order = await getResponse.Content.ReadFromJsonAsync<OrderDetailResponse>();
        order!.ShippingAddress!.Street.Should().Be("New Street 456");
        order.ShippingAddress.City.Should().Be("NewCity");
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Order_Does_Not_Exist()
    {
        // Arrange
        var updateRequest = new
        {
            Street = "St",
            City = "City",
            State = "State",
            Country = "Country",
            ZipCode = "00000"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/orders/{Guid.NewGuid()}/shipping-address", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
