namespace DigitalLeagueTask.Domain;

/// <summary>Курсы всех валют, действующие на дату.</summary>
/// <param name="RateDate">Фактическая дата установления курсов (может отличаться от запрошенной).</param>
/// <param name="Rates">Курсы валют.</param>
public sealed record DailyRates(DateOnly RateDate, IReadOnlyList<CurrencyRate> Rates);
