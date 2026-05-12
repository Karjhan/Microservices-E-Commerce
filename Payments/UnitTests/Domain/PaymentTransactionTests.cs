using Domain.Entities;
using Domain.Enums;
using FluentAssertions;

namespace UnitTests.Domain;

public class PaymentTransactionTests
{
    [Fact]
    public void Constructor_Should_Set_Properties()
    {
        // Arrange & Act
        var tx = new PaymentTransaction(
            Guid.NewGuid(),
            TransactionType.Charge,
            50m,
            "pi_123",
            true,
            null);

        // Assert
        tx.Id.Should().NotBeEmpty();
        tx.Type.Should().Be(TransactionType.Charge);
        tx.Amount.Should().Be(50m);
        tx.ProviderTransactionId.Should().Be("pi_123");
        tx.Success.Should().BeTrue();
        tx.FailureReason.Should().BeNull();
        tx.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_Should_Set_FailureReason_When_Failed()
    {
        var tx = new PaymentTransaction(
            Guid.NewGuid(),
            TransactionType.Charge,
            50m,
            null,
            false,
            "card_declined");

        tx.Success.Should().BeFalse();
        tx.FailureReason.Should().Be("card_declined");
    }

    [Fact]
    public void Constructor_Should_Create_Unique_Ids()
    {
        var tx1 = new PaymentTransaction(Guid.NewGuid(), TransactionType.Charge, 10m, null, true);
        var tx2 = new PaymentTransaction(Guid.NewGuid(), TransactionType.Refund, 10m, null, true);

        tx1.Id.Should().NotBe(tx2.Id);
    }
}
