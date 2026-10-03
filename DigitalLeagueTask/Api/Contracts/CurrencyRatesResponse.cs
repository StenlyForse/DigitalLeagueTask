using DigitalLeagueTask.Domain;

namespace DigitalLeagueTask.Api.Contracts;

/// <summary>Курсы валют на дату.</summary>
/// <param name="RequestedDate">Запрошенная дата.</param>
/// <param name="RateDate">Фактическая дата установления курсов Банком России.</param>
/// <param name="Rates">Курсы валют.</param>
public sealed record CurrencyRatesResponse(
    DateOnly RequestedDate,
    DateOnly RateDate,
    IReadOnlyList<CurrencyRateDto> Rates)
{
    public static CurrencyRatesResponse From(DateOnly requestedDate, DailyRates rates) =>
        new(requestedDate, rates.RateDate, rates.Rates.Select(CurrencyRateDto.From).ToList());
}
