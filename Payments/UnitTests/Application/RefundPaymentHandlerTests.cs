using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Features.Payments.RefundPayment;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class RefundPaymentHandlerTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IPaymentProvider> _provider = new();
    private readonly Mock<ILogger<RefundPaymentHandler>> _logger = new();

    private readonly RefundPaymentHandler _handler;

    public RefundPaymentHandlerTests()
    {
        _handler = new RefundPaymentHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object,
            _provider.Object,
            _logger.Object);
    }

    private void SetupCommonMocks(Payment payment)
    {
        _repo.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _repo.Setup(x => x.AddTransactionAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Should_Refund_Full_Amount_When_No_Amount_Specified()
    {
        // Arrange
        var payment = TestDataBuilder.CreateCompletedPayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.RefundAsync(
                payment.ProviderPaymentId!, payment.Amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderRefundResult(true, "re_123", null));

        // Act
        await _handler.Handle(new RefundPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Should().Be(payment.Amount);
    }

    [Fact]
    public async Task Should_Refund_Partial_Amount()
    {
        // Arrange
        var payment = TestDataBuilder.CreateCompletedPayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.RefundAsync(
                payment.ProviderPaymentId!, 40m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderRefundResult(true, "re_456", null));

        // Act
        await _handler.Handle(new RefundPaymentCommand(payment.Id, 40m), CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Should().Be(40m);
    }

    [Fact]
    public async Task Should_Throw_When_Payment_Not_Found()
    {
        // Arrange
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        // Act & Assert
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => _handler.Handle(new RefundPaymentCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Throw_When_Provider_Refund_Fails()
    {
        // Arrange
        var payment = TestDataBuilder.CreateCompletedPayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.RefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderRefundResult(false, null, "refund_failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidRefundException>(
            () => _handler.Handle(new RefundPaymentCommand(payment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Publish_PaymentRefunded_Event()
    {
        // Arrange
        var payment = TestDataBuilder.CreateCompletedPayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.RefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderRefundResult(true, "re_789", null));

        // Act
        await _handler.Handle(new RefundPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        _publisher.Verify(
            x => x.PublishAsync(
                It.Is<PaymentRefunded>(e =>
                    e.PaymentId == payment.Id &&
                    e.IsFullRefund),
                "payment.refunded"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Create_Refund_Transaction()
    {
        // Arrange
        var payment = TestDataBuilder.CreateCompletedPayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.RefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderRefundResult(true, "re_100", null));

        // Act
        await _handler.Handle(new RefundPaymentCommand(payment.Id, 50m), CancellationToken.None);

        // Assert
        _repo.Verify(x => x.AddTransactionAsync(
            It.Is<PaymentTransaction>(t =>
                t.Type == TransactionType.Refund &&
                t.Amount == 50m &&
                t.Success),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Invalidate_Cache_On_Refund()
    {
        // Arrange
        var payment = TestDataBuilder.CreateCompletedPayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.RefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderRefundResult(true, "re_200", null));

        // Act
        await _handler.Handle(new RefundPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        _cache.Verify(x => x.RemoveAsync($"payment:{payment.Id}"), Times.Once);
        _cache.Verify(x => x.RemoveAsync($"payment:order:{payment.OrderId}"), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("payments:"), Times.Once);
    }
}
