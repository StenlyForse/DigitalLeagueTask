namespace DigitalLeagueTask.Infrastructure.Cbr;

/// <summary>Источник данных ЦБ недоступен, вернул ошибку или некорректные данные (→ 502).</summary>
public sealed class CbrUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
