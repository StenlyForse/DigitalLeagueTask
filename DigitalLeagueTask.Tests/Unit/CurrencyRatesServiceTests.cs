using DigitalLeagueTask.Application;
using DigitalLeagueTask.Domain;
using DigitalLeagueTask.Infrastructure.Cbr;
using DigitalLeagueTask.Tests.Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DigitalLeagueTask.Tests.Unit;

public sealed class CurrencyRatesServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly DateOnly PastDate = new(2026, 9, 27);

    // 2026-10-01 12:00 МСК
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeCbrClient _cbr = new();
    private readonly MemoryCache _cache;
    private readonly CurrencyRatesService _service;

    public CurrencyRatesServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { Clock = new TimeProviderSystemClock(_time) });
        _service = new CurrencyRatesService(
            _cbr,
            _cache,
            new MoscowClock(_time),
            Options.Create(new RatesCacheOptions()),
            NullLogger<CurrencyRatesService>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Returns_all_currencies_when_code_is_not_passed()
    {
        // FR-1
        var result = await _service.GetRatesAsync(Today, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(["USD", "EUR", "JPY"], result.Rates.Select(r => r.CharCode));
    }

    [Theory]
    [InlineData("USD")] // AC-3.1
    [InlineData("usd")] // AC-3.2
    public async Task Returns_single_currency_by_code_case_insensitive(string code)
    {
        var result = await _service.GetRatesAsync(Today, code, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("USD", Assert.Single(result.Rates).CharCode);
        Assert.Equal(Today, result.RateDate);
    }

    [Fact]
    public async Task Returns_null_when_currency_is_absent()
    {
        // FR-4, AC-4.1
        var result = await _service.GetRatesAsync(Today, "XAU", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Does_not_call_cbr_again_for_cached_past_date_with_any_code()
    {
        // NFR-1, AC-N1.1
        await _service.GetRatesAsync(PastDate, null, CancellationToken.None);
        await _service.GetRatesAsync(PastDate, "USD", CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(23));
        await _service.GetRatesAsync(PastDate, "XAU", CancellationToken.None);

        Assert.Equal(1, _cbr.CallCount);
    }

    [Fact]
    public async Task Calls_cbr_again_when_current_date_ttl_expires()
    {
        // NFR-1, AC-N1.2: TTL текущей даты — 15 минут.
        await _service.GetRatesAsync(Today, null, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(14));
        await _service.GetRatesAsync(Today, null, CancellationToken.None);
        Assert.Equal(1, _cbr.CallCount);

        _time.Advance(TimeSpan.FromMinutes(2));
        await _service.GetRatesAsync(Today, null, CancellationToken.None);
        Assert.Equal(2, _cbr.CallCount);
    }

    [Fact]
    public async Task Uses_short_ttl_for_tomorrow()
    {
        var tomorrow = Today.AddDays(1);
        await _service.GetRatesAsync(tomorrow, null, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(16));
        await _service.GetRatesAsync(tomorrow, null, CancellationToken.None);

        Assert.Equal(2, _cbr.CallCount);
    }

    [Fact]
    public async Task Does_not_cache_cbr_errors()
    {
        _cbr.NextError = new CbrUnavailableException("ЦБ недоступен");
        await Assert.ThrowsAsync<CbrUnavailableException>(() => _service.GetRatesAsync(Today, null, CancellationToken.None));

        var result = await _service.GetRatesAsync(Today, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, _cbr.CallCount);
    }

    private sealed class FakeCbrClient : ICbrRatesClient
    {
        public int CallCount { get; private set; }

        public Exception? NextError { get; set; }

        public Task<DailyRates> GetDailyRatesAsync(DateOnly date, CancellationToken cancellationToken)
        {
            CallCount++;
            if (NextError is { } error)
            {
                NextError = null;
                return Task.FromException<DailyRates>(error);
            }

            return Task.FromResult(new DailyRates(date,
            [
                new CurrencyRate("USD", "840", "Доллар США", 1, 83.5588m, 83.5588m),
                new CurrencyRate("EUR", "978", "Евро", 1, 97.9811m, 97.9811m),
                new CurrencyRate("JPY", "392", "Иен", 100, 53.1038m, 0.531038m),
            ]));
        }
    }
}
