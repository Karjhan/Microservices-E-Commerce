using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class ExpiredPaymentJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredPaymentJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public ExpiredPaymentJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredPaymentJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredPaymentJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredPayments(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired payments");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredPayments(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var expiredPayments = await repo.GetExpiredPendingPaymentsAsync(ct);

        if (expiredPayments.Count == 0)
            return;

        _logger.LogInformation("Found {Count} expired pending payments", expiredPayments.Count);

        foreach (var payment in expiredPayments)
        {
            payment.MarkCancelled();

            await publisher.PublishAsync(new PaymentCancelled
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                UserId = payment.UserId,
                Reason = "Payment expired (auto-cancelled)",
                CancelledAt = DateTime.UtcNow
            }, "payment.cancelled");

            await cache.RemoveAsync($"payment:{payment.Id}");
            await cache.RemoveAsync($"payment:order:{payment.OrderId}");

            _logger.LogInformation(
                "Auto-cancelled expired payment {PaymentId} for order {OrderId}",
                payment.Id, payment.OrderId);
        }

        await repo.SaveChangesAsync(ct);
        await cache.RemoveByPrefixAsync("payments:");
    }
}
