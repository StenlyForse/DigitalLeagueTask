using System.ComponentModel.DataAnnotations;

namespace DigitalLeagueTask.Application;

/// <summary>Сроки хранения курсов в памяти (NFR-1, NFR-5).</summary>
public sealed class RatesCacheOptions
{
    public const string SectionName = "RatesCache";

    /// <summary>Для текущей и будущей даты: ЦБ может опубликовать курс позже.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan CurrentDatesTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Для прошедших дат: курсы неизменны.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00")]
    public TimeSpan PastDatesTtl { get; set; } = TimeSpan.FromDays(1);
}
