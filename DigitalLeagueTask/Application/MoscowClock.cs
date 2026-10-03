namespace DigitalLeagueTask.Application;

/// <summary>
/// Текущая дата по московскому времени — курсы ЦБ устанавливаются по МСК (FR-2, A-2).
/// Используется фиксированное смещение UTC+3: с 2014 г. в Москве нет перехода на летнее время,
/// а зависимость от tzdata в контейнере не нужна (design.md, ADR-6).
/// </summary>
public sealed class MoscowClock(TimeProvider timeProvider)
{
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(3);

    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(UtcOffset).DateTime);
}
