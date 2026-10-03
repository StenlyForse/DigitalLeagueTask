using DigitalLeagueTask.Domain;

namespace DigitalLeagueTask.Application;

/// <summary>Получение курсов валют на дату.</summary>
public interface ICurrencyRatesService
{
    /// <summary>Курсы на дату: все валюты или одна по коду.</summary>
    /// <param name="date">Запрошенная дата.</param>
    /// <param name="code">Буквенный код валюты (регистр не важен); <c>null</c> — все валюты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Курсы; <c>null</c>, если код передан, а валюты нет в данных ЦБ на дату (FR-4).</returns>
    Task<DailyRates?> GetRatesAsync(DateOnly date, string? code, CancellationToken cancellationToken);
}
