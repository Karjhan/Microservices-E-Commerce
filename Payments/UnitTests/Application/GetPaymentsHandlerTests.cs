using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Application.Features.Payments.GetPayments;
using Domain.Entities;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetPaymentsHandlerTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetPaymentsHandler _handler;

    public GetPaymentsHandlerTests()
    {
        _handler = new GetPaymentsHandler(_repo.Object, _cache.Object);
    }

    [Fact]
    public async Task Should_Return_From_Cache()
    {
        // Arrange
        var list = new List<PaymentDto>
        {
            PaymentDto.FromEntity(TestDataBuilder.CreatePayment())
        };

        _cache.Setup(x => x.GetAsync<List<PaymentDto>>(It.IsAny<string>()))
            .ReturnsAsync(list);

        // Act
        var result = await _handler.Handle(
            new GetPaymentsQuery(new PaymentFilter()),
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Query_Repository_When_No_Cache()
    {
        // Arrange
        _cache.Setup(x => x.GetAsync<List<PaymentDto>>(It.IsAny<string>()))
            .ReturnsAsync((List<PaymentDto>?)null);

        _repo.Setup(x => x.GetFilteredAsync(It.IsAny<PaymentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Payment>());

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(
            new GetPaymentsQuery(new PaymentFilter()),
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Return_Mapped_Results_From_Repository()
    {
        // Arrange
        var payments = new List<Payment>
        {
            TestDataBuilder.CreatePayment(),
            TestDataBuilder.CreatePayment()
        };

        _cache.Setup(x => x.GetAsync<List<PaymentDto>>(It.IsAny<string>()))
            .ReturnsAsync((List<PaymentDto>?)null);

        _repo.Setup(x => x.GetFilteredAsync(It.IsAny<PaymentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payments);

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(
            new GetPaymentsQuery(new PaymentFilter()),
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }
}
