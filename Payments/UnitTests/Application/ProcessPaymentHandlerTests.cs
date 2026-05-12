using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Features.Payments.ProcessPayment;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class ProcessPaymentHandlerTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IPaymentProvider> _provider = new();
    private readonly Mock<ILogger<ProcessPaymentHandler>> _logger = new();

    private readonly ProcessPaymentHandler _handler;

    public ProcessPaymentHandlerTests()
    {
        _handler = new ProcessPaymentHandler(
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
    public async Task Should_Complete_Payment_When_Charge_Succeeds()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.ChargeAsync(
                payment.PaymentToken!, payment.Amount, payment.Currency,
                payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderChargeResult(true, "pi_stripe_123", null));

        // Act
        await _handler.Handle(new ProcessPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.ProviderPaymentId.Should().Be("pi_stripe_123");
        payment.Transactions.Should().ContainSingle();

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<PaymentCompleted>(), "payment.completed"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Fail_Payment_When_Charge_Fails()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.ChargeAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderChargeResult(false, null, "card_declined"));

        // Act
        await _handler.Handle(new ProcessPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("card_declined");

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<PaymentFailed>(), "payment.failed"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Throw_When_Payment_Not_Found()
    {
        // Arrange
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        // Act & Assert
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => _handler.Handle(new ProcessPaymentCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Throw_When_Payment_Has_No_Token()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment(paymentToken: null);

        _repo.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidPaymentStateException>(
            () => _handler.Handle(new ProcessPaymentCommand(payment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Invalidate_Cache_On_Completion()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.ChargeAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderChargeResult(true, "pi_123", null));

        // Act
        await _handler.Handle(new ProcessPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        _cache.Verify(x => x.RemoveAsync($"payment:{payment.Id}"), Times.Once);
        _cache.Verify(x => x.RemoveAsync($"payment:order:{payment.OrderId}"), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("payments:"), Times.Once);
    }

    [Fact]
    public async Task Should_Create_Transaction_Record()
    {
        // Arrange
        var payment = TestDataBuilder.CreatePayment();
        SetupCommonMocks(payment);

        _provider.Setup(x => x.ChargeAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderChargeResult(true, "pi_123", null));

        // Act
        await _handler.Handle(new ProcessPaymentCommand(payment.Id), CancellationToken.None);

        // Assert
        _repo.Verify(x => x.AddTransactionAsync(
            It.Is<PaymentTransaction>(t =>
                t.Type == TransactionType.Charge &&
                t.Amount == payment.Amount &&
                t.Success),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
