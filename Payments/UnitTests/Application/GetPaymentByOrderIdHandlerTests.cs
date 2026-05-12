using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Application.Features.Payments.GetPaymentByOrderId;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetPaymentByOrderIdHandlerTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetPaymentByOrderIdHandler _handler;

    public GetPaymentByOrderIdHandlerTests()
    {
        _handler = new GetPaymentByOrderIdHandler(_repo.Object, _cache.Object);
    }

    [Fact]
    public async Task Should_Return_From_Cache()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();
        var dto = PaymentDto.FromEntity(payment);

        _cache.Setup(x => x.GetAsync<PaymentDto>($"payment:order:{payment.OrderId}"))
            .ReturnsAsync(dto);

        // Act
        var result = await _handler.Handle(
            new GetPaymentByOrderIdQuery(payment.OrderId),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(payment.OrderId);

        _repo.Verify(x => x.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_Load_From_Db_When_Not_Cached()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();

        _cache.Setup(x => x.GetAsync<PaymentDto>(It.IsAny<string>()))
            .ReturnsAsync((PaymentDto?)null);

        _repo.Setup(x => x.GetByOrderIdAsync(payment.OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<PaymentDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(
            new GetPaymentByOrderIdQuery(payment.OrderId),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Throw_When_Not_Found()
    {
        // Arrange
        _cache.Setup(x => x.GetAsync<PaymentDto>(It.IsAny<string>()))
            .ReturnsAsync((PaymentDto?)null);

        _repo.Setup(x => x.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((global::Domain.Entities.Payment?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _handler.Handle(new GetPaymentByOrderIdQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
