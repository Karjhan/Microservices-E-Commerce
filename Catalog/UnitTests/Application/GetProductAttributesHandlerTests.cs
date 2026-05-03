using Application.Features.Products.GetAttributes;
using FluentAssertions;
using Infrastructure.Abstractions.Persistence;
using Moq;

namespace UnitTests.Application;

public class GetProductAttributesHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();

    private readonly GetProductAttributesHandler _handler;

    public GetProductAttributesHandlerTests()
    {
        _handler = new GetProductAttributesHandler(_repo.Object);
    }

    [Fact]
    public async Task Should_Return_Attributes()
    {
        var productId = Guid.NewGuid();

        _repo.Setup(x => x.GetAttributesAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Commons.ProductAttribute>
            {
                new(productId, "color", "red")
            });

        var result = await _handler.Handle(
            new GetProductAttributesQuery(productId),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("color");
    }
}