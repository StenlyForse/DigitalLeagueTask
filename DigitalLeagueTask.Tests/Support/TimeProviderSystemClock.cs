using Microsoft.Extensions.Internal;

namespace DigitalLeagueTask.Tests.Support;

/// <summary>
/// Часы MemoryCache поверх TimeProvider: в .NET 8 MemoryCacheOptions.Clock ещё не принимает
/// TimeProvider, а истечение кэша (AC-N1.2) нужно проверять на FakeTimeProvider.
/// </summary>
internal sealed class TimeProviderSystemClock(TimeProvider timeProvider) : ISystemClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
