namespace DigitalLeagueTask.Infrastructure.Cbr;

/// <summary>Источник данных ЦБ не ответил за отведённое время (→ 504).</summary>
public sealed class CbrTimeoutException(string message, Exception? innerException = null)
    : Exception(message, innerException);
