using Microsoft.Extensions.Options;
using Shared.Security.Configuration;

namespace Shared.Security.Time;

public readonly record struct UtcRange(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed class StoreCalendar
{
    private readonly TimeZoneInfo _zone;

    public StoreCalendar(IOptions<StoreOptions> options)
        : this(options.Value.TimeZone)
    {
    }

    public StoreCalendar(string timeZoneId)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public TimeZoneInfo Zone => _zone;

    public DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (_zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var offset = _zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public UtcRange ToUtcRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentException("'to' must not be before 'from'.", nameof(to));
        }

        return new UtcRange(StartOfDayUtc(from), StartOfDayUtc(to.AddDays(1)));
    }

    public (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentException("'to' must not be before 'from'.", nameof(to));
        }

        var days = to.DayNumber - from.DayNumber + 1;
        return (from.AddDays(-days), from.AddDays(-1));
    }

    public DateOnly LocalDate(DateTimeOffset utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, _zone).DateTime);
}
