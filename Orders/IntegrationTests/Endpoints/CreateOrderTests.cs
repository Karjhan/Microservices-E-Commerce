using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class CreateOrderTests : IntegrationTestBase
{
    public CreateOrderTests(OrdersApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Create_Order()
    {
        // Arrange
        var request = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            Notes = "Test order",
            RecipientEmail = "test@example.com",
            ShippingAddress = new
            {
                Street = "123 Main St",
                City = "Bucharest",
                State = "Ilfov",
                Country = "Romania",
                ZipCode = "010101",
                FullName = "John Doe",
                Phone = "+40712345678"
            },
            Items = new[]
            {
                new
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Headphones",
                    UnitPrice = 49.99m,
                    Quantity = 2,
                    Currency = "EUR",
                    ProductImageUrl = (string?)null
                }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.OrderId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Create_Order_With_Multiple_Items()
    {
        // Arrange
        var request = new
        {
            UserId = Guid.NewGuid(),
            Currency = "EUR",
            ShippingAddress = new
            {
                Street = "456 Elm St",
                City = "Cluj",
                State = "Cluj",
                Country = "Romania",
                ZipCode = "400001"
            },
            Items = new[]
            {
                new { ProductId = Guid.NewGuid(), ProductName = "Item A", UnitPrice = 10m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null },
                new { ProductId = Guid.NewGuid(), ProductName = "Item B", UnitPrice = 20m, Quantity = 3, Currency = "EUR", ProductImageUrl = (string?)null }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
