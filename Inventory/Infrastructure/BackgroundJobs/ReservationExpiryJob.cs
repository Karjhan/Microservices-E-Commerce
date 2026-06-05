using Application.Abstractions.Persistence;
using Application.Features.Inventory.ReleaseStock;
using Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

public class ReservationExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryJob> _logger;
    private readonly InventoryOptions _options;

    public ReservationExpiryJob(
        IServiceScopeFactory scopeFactory,
        IOptions<InventoryOptions> options,
        ILogger<ReservationExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ReservationExpiryJob started (interval={IntervalSeconds}s, batch={BatchSize})",
            _options.ExpiryJobIntervalSeconds, _options.ExpiryJobBatchSize);

        var interval = TimeSpan.FromSeconds(_options.ExpiryJobIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredReservations(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired reservations");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredReservations(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var repo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var asOf = DateTime.UtcNow;
        var expired = await repo.GetActiveExpiredReservationsAsync(asOf, _options.ExpiryJobBatchSize, ct);

        if (expired.Count == 0)
            return;

        _logger.LogInformation(
            "Found {Count} expired active reservations as of {AsOf:O}",
            expired.Count, asOf);

        var orderIds = expired.Select(r => r.OrderId).Distinct().ToList();

        foreach (var orderId in orderIds)
        {
            try
            {
                var result = await mediator.Send(
                    new ReleaseStockCommand(
                        OrderId: orderId,
                        Reason: "reservation.expired",
                        CorrelationId: null,
                        MarkExpired: true),
                    ct);

                _logger.LogInformation(
                    "Expired order {OrderId}: released={Released} note={Note}",
                    orderId, result.ReleasedCount, result.Note ?? "ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to expire reservations for order {OrderId}",
                    orderId);
            }
        }
    }
}
