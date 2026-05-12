using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;

namespace UnitTests.Domain;

public class PaymentTests
{
    [Fact]
    public void Constructor_Should_Set_Properties()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var payment = new Payment(orderId, userId, 50m, "USD", PaymentMethod.CreditCard, "key-1", "pm_token");

        // Assert
        payment.Id.Should().NotBeEmpty();
        payment.OrderId.Should().Be(orderId);
        payment.UserId.Should().Be(userId);
        payment.Amount.Should().Be(50m);
        payment.Currency.Should().Be("USD");
        payment.Method.Should().Be(PaymentMethod.CreditCard);
        payment.IdempotencyKey.Should().Be("key-1");
        payment.PaymentToken.Should().Be("pm_token");
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.RefundedAmount.Should().Be(0);
        payment.ExpiresAt.Should().BeAfter(payment.CreatedAt);
    }

    [Fact]
    public void MarkProcessing_Should_Transition_From_Pending()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");

        payment.MarkProcessing();

        payment.Status.Should().Be(PaymentStatus.Processing);
        payment.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessing_Should_Throw_When_Not_Pending()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();

        var act = () => payment.MarkProcessing();

        act.Should().Throw<InvalidPaymentStateException>();
    }

    [Fact]
    public void MarkCompleted_Should_Transition_From_Processing()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();

        payment.MarkCompleted("pi_123");

        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.ProviderPaymentId.Should().Be("pi_123");
        payment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_Should_Throw_When_Not_Processing()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");

        var act = () => payment.MarkCompleted("pi_123");

        act.Should().Throw<InvalidPaymentStateException>();
    }

    [Fact]
    public void MarkFailed_Should_Transition_From_Processing()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();

        payment.MarkFailed("insufficient_funds");

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("insufficient_funds");
    }

    [Fact]
    public void MarkFailed_Should_Throw_When_Not_Processing()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");

        var act = () => payment.MarkFailed("reason");

        act.Should().Throw<InvalidPaymentStateException>();
    }

    [Fact]
    public void MarkCancelled_Should_Transition_From_Pending()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");

        payment.MarkCancelled();

        payment.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void MarkCancelled_Should_Throw_When_Not_Pending()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();

        var act = () => payment.MarkCancelled();

        act.Should().Throw<InvalidPaymentStateException>();
    }

    [Fact]
    public void ApplyRefund_Should_Set_PartiallyRefunded_For_Partial()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();
        payment.MarkCompleted("pi_1");

        payment.ApplyRefund(40m);

        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Should().Be(40m);
    }

    [Fact]
    public void ApplyRefund_Should_Set_Refunded_For_Full()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();
        payment.MarkCompleted("pi_1");

        payment.ApplyRefund(100m);

        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Should().Be(100m);
    }

    [Fact]
    public void ApplyRefund_Should_Allow_Multiple_Partial_Refunds()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();
        payment.MarkCompleted("pi_1");

        payment.ApplyRefund(30m);
        payment.ApplyRefund(70m);

        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Should().Be(100m);
    }

    [Fact]
    public void ApplyRefund_Should_Throw_When_Amount_Exceeds_Remaining()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();
        payment.MarkCompleted("pi_1");

        var act = () => payment.ApplyRefund(101m);

        act.Should().Throw<InvalidRefundException>();
    }

    [Fact]
    public void ApplyRefund_Should_Throw_When_Amount_Is_Zero()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");
        payment.MarkProcessing();
        payment.MarkCompleted("pi_1");

        var act = () => payment.ApplyRefund(0m);

        act.Should().Throw<InvalidRefundException>();
    }

    [Fact]
    public void ApplyRefund_Should_Throw_When_Not_Completed()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");

        var act = () => payment.ApplyRefund(10m);

        act.Should().Throw<InvalidRefundException>();
    }

    [Fact]
    public void AddTransaction_Should_Add_To_Collection()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");
        var tx = new PaymentTransaction(payment.Id, TransactionType.Charge, 100m, "pi_1", true);

        payment.AddTransaction(tx);

        payment.Transactions.Should().ContainSingle();
    }

    [Fact]
    public void IsExpired_Should_Be_False_When_Not_Expired()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", PaymentMethod.CreditCard, "k", "t");

        payment.IsExpired.Should().BeFalse();
    }
}
