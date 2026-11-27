using FluentAssertions;
using Shared.Security.Time;

namespace Shared.Security.Tests.Time;

public class StoreCalendarTests
{
    private readonly StoreCalendar _calendar = new("Europe/Bucharest");

    [Fact]
    public void Winter_day_starts_at_22_utc_the_previous_day()
    {
        // Bucharest is UTC+2 in winter.
        var start = _calendar.StartOfDayUtc(new DateOnly(2026, 1, 15));

        start.Should().Be(new DateTimeOffset(2026, 1, 14, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Summer_day_starts_at_21_utc_the_previous_day()
    {
        // Bucharest is UTC+3 in summer.
        var start = _calendar.StartOfDayUtc(new DateOnly(2026, 7, 15));

        start.Should().Be(new DateTimeOffset(2026, 7, 14, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Range_across_spring_forward_is_23_hours_long_for_the_changeover_day()
    {
        // DST starts on the last Sunday of March (2026-03-29).
        var range = _calendar.ToUtcRange(new DateOnly(2026, 3, 29), new DateOnly(2026, 3, 29));

        (range.EndUtc - range.StartUtc).Should().Be(TimeSpan.FromHours(23));
    }

    [Fact]
    public void Range_across_fall_back_is_25_hours_long_for_the_changeover_day()
    {
        // DST ends on the last Sunday of October (2026-10-25).
        var range = _calendar.ToUtcRange(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 25));

        (range.EndUtc - range.StartUtc).Should().Be(TimeSpan.FromHours(25));
    }

    [Fact]
    public void Range_is_half_open_and_contiguous_between_consecutive_days()
    {
        var day1 = _calendar.ToUtcRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 1));
        var day2 = _calendar.ToUtcRange(new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 2));

        day1.EndUtc.Should().Be(day2.StartUtc);
    }

    [Fact]
    public void Previous_period_has_the_same_length_and_ends_the_day_before()
    {
        var (from, to) = _calendar.PreviousPeriod(new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 14));

        from.Should().Be(new DateOnly(2026, 6, 1));
        to.Should().Be(new DateOnly(2026, 6, 7));
    }

    [Fact]
    public void Previous_period_of_a_single_day_is_the_day_before()
    {
        var (from, to) = _calendar.PreviousPeriod(new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 8));

        from.Should().Be(new DateOnly(2026, 6, 7));
        to.Should().Be(new DateOnly(2026, 6, 7));
    }

    [Fact]
    public void To_before_from_is_rejected()
    {
        var act = () => _calendar.ToUtcRange(new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 7));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Local_date_uses_the_store_zone()
    {
        // 22:30 UTC on 14 Jan is already 15 Jan in Bucharest.
        var date = _calendar.LocalDate(new DateTimeOffset(2026, 1, 14, 22, 30, 0, TimeSpan.Zero));

        date.Should().Be(new DateOnly(2026, 1, 15));
    }
}
