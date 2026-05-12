using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public class PaymentRepositoryTests
{
    private static PaymentsDbContext CreateDbContext(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseSqlite($"Filename={name};Mode=Memory;Cache=Shared")
            .Options;

        var context = new PaymentsDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }

    [Fact]
    public async Task AddAsync_ShouldPersistPayment()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR",
            PaymentMethod.CreditCard, "key-1", "pm_token");

        // Act
        await repo.AddAsync(payment, default);
        await repo.SaveChangesAsync(default);

        // Assert
        context.Payments.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPaymentWithTransactions()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var writeContext = CreateDbContext(dbName);
        var writeRepo = new PaymentRepository(writeContext);

        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR",
            PaymentMethod.CreditCard, "key-2", "pm_token");

        var tx = new PaymentTransaction(payment.Id, TransactionType.Charge, 100m, "pi_1", true);

        await writeRepo.AddAsync(payment, default);
        await writeRepo.AddTransactionAsync(tx, default);
        payment.AddTransaction(tx);
        await writeRepo.SaveChangesAsync(default);

        // Act — use a fresh context for the read
        var readContext = CreateDbContext(dbName);
        var readRepo = new PaymentRepository(readContext);
        var result = await readRepo.GetByIdAsync(payment.Id, default);

        // Assert
        result.Should().NotBeNull();
        result!.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByOrderIdAsync_ShouldReturnPayment()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        var orderId = Guid.NewGuid();
        var payment = new Payment(orderId, Guid.NewGuid(), 50m, "USD",
            PaymentMethod.DebitCard, "key-3");

        await repo.AddAsync(payment, default);
        await repo.SaveChangesAsync(default);

        // Act
        var result = await repo.GetByOrderIdAsync(orderId, default);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_ShouldReturnPayment()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 75m, "EUR",
            PaymentMethod.CreditCard, "unique-key-99");

        await repo.AddAsync(payment, default);
        await repo.SaveChangesAsync(default);

        // Act
        var result = await repo.GetByIdempotencyKeyAsync("unique-key-99", default);

        // Assert
        result.Should().NotBeNull();
        result!.IdempotencyKey.Should().Be("unique-key-99");
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByStatus()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        var p1 = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR",
            PaymentMethod.CreditCard, "k1", "t1");
        var p2 = new Payment(Guid.NewGuid(), Guid.NewGuid(), 20m, "EUR",
            PaymentMethod.CreditCard, "k2", "t2");

        p2.MarkProcessing();
        p2.MarkCompleted("pi_completed");

        await repo.AddAsync(p1, default);
        await repo.AddAsync(p2, default);
        await repo.SaveChangesAsync(default);

        var filter = new PaymentFilter { Status = PaymentStatus.Completed };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
        result.First().Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByUserId()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        var userId = Guid.NewGuid();
        var p1 = new Payment(Guid.NewGuid(), userId, 10m, "EUR",
            PaymentMethod.CreditCard, "k1");
        var p2 = new Payment(Guid.NewGuid(), Guid.NewGuid(), 20m, "EUR",
            PaymentMethod.CreditCard, "k2");

        await repo.AddAsync(p1, default);
        await repo.AddAsync(p2, default);
        await repo.SaveChangesAsync(default);

        var filter = new PaymentFilter { UserId = userId };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be(userId);
    }

    [Fact]
    public async Task AddTransactionAsync_ShouldPersistTransaction()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR",
            PaymentMethod.CreditCard, "k-tx", "pm_token");

        await repo.AddAsync(payment, default);
        await repo.SaveChangesAsync(default);

        var tx = new PaymentTransaction(payment.Id, TransactionType.Charge, 100m, "pi_1", true);

        // Act
        await repo.AddTransactionAsync(tx, default);
        await repo.SaveChangesAsync(default);

        // Assert
        context.PaymentTransactions.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldPaginate()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new PaymentRepository(context);

        for (int i = 0; i < 5; i++)
        {
            var p = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m + i, "EUR",
                PaymentMethod.CreditCard, $"page-key-{i}");
            await repo.AddAsync(p, default);
        }

        await repo.SaveChangesAsync(default);

        var filter = new PaymentFilter { Page = 1, PageSize = 2 };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(2);
    }
}
