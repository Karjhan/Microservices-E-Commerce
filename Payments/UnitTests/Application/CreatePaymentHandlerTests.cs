using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Payments.CreatePayment;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class CreatePaymentHandlerTests
{
    private readonly Mock<IPaymentRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly CreatePaymentHandler _handler;

    public CreatePaymentHandlerTests()
    {
        _handler = new CreatePaymentHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Create_Payment_And_Return_Id()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "EUR",
            PaymentMethod.CreditCard,
            "idem-key-1",
            "pm_test_token");

        _repo.Setup(x => x.GetByIdempotencyKeyAsync("idem-key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        _repo.Setup(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveByPrefixAsync("payments:"))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();

        _repo.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("payments:"), Times.Once);
    }

    [Fact]
    public async Task Should_Throw_When_Duplicate_IdempotencyKey()
    {
        // Arrange
        var existing = TestDataBuilder.CreatePayment();

        _repo.Setup(x => x.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "EUR",
            PaymentMethod.CreditCard,
            existing.IdempotencyKey,
            "pm_token");

        // Act & Assert
        await Assert.ThrowsAsync<DuplicatePaymentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Should_Publish_PaymentCreated_Event()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            PaymentMethod.DebitCard,
            "key-2",
            "pm_token");

        _repo.Setup(x => x.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        _repo.Setup(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<Contracts.Events.PaymentCreated>(), "payment.created"),
            Times.Once);
    }
}
