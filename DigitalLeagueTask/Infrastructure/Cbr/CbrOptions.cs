using System.ComponentModel.DataAnnotations;

namespace DigitalLeagueTask.Infrastructure.Cbr;

/// <summary>Настройки обращения к сайту Банка России (NFR-2, NFR-5).</summary>
public sealed class CbrOptions
{
    public const string SectionName = "Cbr";

    /// <summary>Базовый адрес; должен заканчиваться на «/».</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://www.cbr.ru/");

    [Required]
    public string DailyRatesPath { get; set; } = "scripts/XML_daily.asp";

    /// <summary>Таймаут одной попытки.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:05:00")]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Общий таймаут с учётом всех повторов.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:10:00")]
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(15);

    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Базовая задержка между повторами (экспоненциальный рост с jitter).</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
