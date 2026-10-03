using DigitalLeagueTask.Domain;

namespace DigitalLeagueTask.Infrastructure.Cbr;

/// <summary>Получение курсов с сайта Банка России.</summary>
public interface ICbrRatesClient
{
    /// <summary>Курсы всех валют, действующие на дату.</summary>
    /// <exception cref="CbrUnavailableException">ЦБ недоступен, вернул ошибку или некорректные данные.</exception>
    /// <exception cref="CbrTimeoutException">ЦБ не ответил за отведённое время.</exception>
    Task<DailyRates> GetDailyRatesAsync(DateOnly date, CancellationToken cancellationToken);
}
