using Application.Features.Products.GetRelations;
using FluentAssertions;
using Infrastructure.Abstractions.Persistence;
using Moq;

namespace UnitTests.Application;

public class GetProductRelationsHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();

    private readonly GetProductRelationsHandler _handler;

    public GetProductRelationsHandlerTests()
    {
        _handler = new GetProductRelationsHandler(_repo.Object);
    }

    [Fact]
    public async Task Should_Return_Relations()
    {
        var productId = Guid.NewGuid();

        _repo.Setup(x => x.GetRelationsAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Commons.ProductRelation>
            {
                new(productId, Guid.NewGuid(), "similar")
            });

        var result = await _handler.Handle(
            new GetProductRelationsQuery(productId),
            CancellationToken.None);

        result.Should().HaveCount(1);
    }
}