using DigitalLeagueTask.Application;
using Microsoft.Extensions.Time.Testing;

namespace DigitalLeagueTask.Tests.Unit;

public class MoscowClockTests
{
    [Theory]
    // FR-2, AC-2.1: 23:30 UTC — уже 02:30 следующего дня по МСК.
    [InlineData("2026-09-30T23:30:00Z", "2026-10-01")]
    [InlineData("2026-09-30T21:00:00Z", "2026-10-01")]
    [InlineData("2026-09-30T20:59:59Z", "2026-09-30")]
    public void Today_is_calculated_in_moscow_time(string utcNow, string expectedToday)
    {
        var clock = new MoscowClock(new FakeTimeProvider(DateTimeOffset.Parse(utcNow)));

        Assert.Equal(DateOnly.Parse(expectedToday), clock.Today);
    }
}
