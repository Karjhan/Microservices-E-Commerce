using Application.Abstractions.Inventory;
using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public class ReservationPolicy : IReservationPolicy
{
    private readonly InventoryOptions _options;

    public ReservationPolicy(IOptions<InventoryOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan ReservationTimeout => TimeSpan.FromMinutes(_options.ReservationTimeoutMinutes);
}
