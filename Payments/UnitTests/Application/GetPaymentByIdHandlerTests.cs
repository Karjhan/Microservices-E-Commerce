using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Application.Features.Payments.GetPaymentById;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetPaymentByIdHandlerTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetPaymentByIdHandler _handler;

    public GetPaymentByIdHandlerTests()
    {
        _handler = new GetPaymentByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Admin());
    }

    [Fact]
    public async Task Should_Return_From_Cache()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();
        var dto = PaymentDto.FromEntity(payment);

        _cache.Setup(x => x.GetAsync<PaymentDto>($"payment:{payment.Id}"))
            .ReturnsAsync(dto);

        // Act
        var result = await _handler.Handle(
            new GetPaymentByIdQuery(payment.Id),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(payment.Id);

        _repo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_Load_From_Db_When_Not_Cached()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();

        _cache.Setup(x => x.GetAsync<PaymentDto>(It.IsAny<string>()))
            .ReturnsAsync((PaymentDto?)null);

        _repo.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<PaymentDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(
            new GetPaymentByIdQuery(payment.Id),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task Should_Throw_When_Not_Found()
    {
        // Arrange
        _cache.Setup(x => x.GetAsync<PaymentDto>(It.IsAny<string>()))
            .ReturnsAsync((PaymentDto?)null);

        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((global::Domain.Entities.Payment?)null);

        // Act & Assert
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => _handler.Handle(new GetPaymentByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Return_Payment_To_Its_Owner()
    {
        var payment = TestDataBuilder.CreatePayment();
        _cache.Setup(x => x.GetAsync<PaymentDto>(It.IsAny<string>())).ReturnsAsync((PaymentDto?)null);
        _repo.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var handler = new GetPaymentByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(payment.UserId));

        var result = await handler.Handle(new GetPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task Should_Report_Not_Found_For_Non_Owner_When_Loaded_From_Database()
    {
        var payment = TestDataBuilder.CreatePayment();
        _cache.Setup(x => x.GetAsync<PaymentDto>(It.IsAny<string>())).ReturnsAsync((PaymentDto?)null);
        _repo.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var handler = new GetPaymentByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(Guid.NewGuid()));

        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => handler.Handle(new GetPaymentByIdQuery(payment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Report_Not_Found_For_Non_Owner_When_Served_From_Cache()
    {
        var payment = TestDataBuilder.CreatePayment();
        _cache.Setup(x => x.GetAsync<PaymentDto>($"payment:{payment.Id}")).ReturnsAsync(PaymentDto.FromEntity(payment));

        var handler = new GetPaymentByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(Guid.NewGuid()));

        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => handler.Handle(new GetPaymentByIdQuery(payment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Report_Not_Found_For_Anonymous_Caller()
    {
        var payment = TestDataBuilder.CreatePayment();
        _cache.Setup(x => x.GetAsync<PaymentDto>($"payment:{payment.Id}")).ReturnsAsync(PaymentDto.FromEntity(payment));

        var handler = new GetPaymentByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Anonymous());

        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => handler.Handle(new GetPaymentByIdQuery(payment.Id), CancellationToken.None));
    }
}
