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
        var cached = new PaginatedListDto<PaymentDto>
        {
            Items = [PaymentDto.FromEntity(TestDataBuilder.CreatePayment())],
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        _cache.Setup(x => x.GetAsync<PaginatedListDto<PaymentDto>>(It.IsAny<string>()))
            .ReturnsAsync(cached);

        var result = await _handler.Handle(
            new GetPaymentsQuery(new PaymentFilter()),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Query_Repository_When_No_Cache()
    {
        _cache.Setup(x => x.GetAsync<PaginatedListDto<PaymentDto>>(It.IsAny<string>()))
            .ReturnsAsync((PaginatedListDto<PaymentDto>?)null);

        _repo.Setup(x => x.GetFilteredAsync(It.IsAny<PaymentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Payment>());

        _repo.Setup(x => x.CountAsync(It.IsAny<PaymentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new GetPaymentsQuery(new PaymentFilter()),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Return_Mapped_Results_From_Repository()
    {
        var payments = new List<Payment>
        {
            TestDataBuilder.CreatePayment(),
            TestDataBuilder.CreatePayment()
        };

        _cache.Setup(x => x.GetAsync<PaginatedListDto<PaymentDto>>(It.IsAny<string>()))
            .ReturnsAsync((PaginatedListDto<PaymentDto>?)null);

        _repo.Setup(x => x.GetFilteredAsync(It.IsAny<PaymentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payments);

        _repo.Setup(x => x.CountAsync(It.IsAny<PaymentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new GetPaymentsQuery(new PaymentFilter()),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }
}