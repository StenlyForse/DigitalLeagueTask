using DigitalLeagueTask.Domain;
using DigitalLeagueTask.Infrastructure.Cbr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DigitalLeagueTask.Application;

/// <summary>
/// Курсы на дату из кэша или ЦБ с фильтрацией по коду (FR-1, FR-3, FR-4).
/// Кэшируются все валюты на запрошенную дату; ошибки ЦБ не кэшируются (NFR-1; design.md, ADR-4).
/// </summary>
internal sealed class CurrencyRatesService(
    ICbrRatesClient cbrClient,
    IMemoryCache cache,
    MoscowClock clock,
    IOptions<RatesCacheOptions> cacheOptions,
    ILogger<CurrencyRatesService> logger) : ICurrencyRatesService
{
    public async Task<DailyRates?> GetRatesAsync(DateOnly date, string? code, CancellationToken cancellationToken)
    {
        var rates = await GetDailyRatesAsync(date, cancellationToken);
        if (code is null)
        {
            return rates;
        }

        var rate = rates.Rates.FirstOrDefault(r => string.Equals(r.CharCode, code, StringComparison.OrdinalIgnoreCase));
        return rate is null ? null : rates with { Rates = [rate] };
    }

    private async Task<DailyRates> GetDailyRatesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var cacheKey = $"cbr:daily:{date:yyyy-MM-dd}";
        if (cache.TryGetValue(cacheKey, out DailyRates? cached) && cached is not null)
        {
            logger.LogDebug("Курсы на {RequestedDate} взяты из кэша", date);
            return cached;
        }

        logger.LogDebug("Курсов на {RequestedDate} нет в кэше, запрос к ЦБ", date);
        var rates = await cbrClient.GetDailyRatesAsync(date, cancellationToken);

        var ttl = date < clock.Today ? cacheOptions.Value.PastDatesTtl : cacheOptions.Value.CurrentDatesTtl;
        cache.Set(cacheKey, rates, ttl);

        return rates;
    }
}
