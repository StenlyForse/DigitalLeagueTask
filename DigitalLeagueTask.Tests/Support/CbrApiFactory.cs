using DigitalLeagueTask.Application;
using DigitalLeagueTask.Infrastructure.Cbr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DigitalLeagueTask.Tests.Support;

/// <summary>
/// Приложение целиком, но вместо сайта ЦБ — <see cref="StubCbrHandler"/> (design.md, ADR-9).
/// Resilience-pipeline реальный, с уменьшенными таймаутами.
/// </summary>
internal sealed class CbrApiFactory(DateTimeOffset? utcNow = null) : WebApplicationFactory<Program>
{
    /// <summary>2026-10-01 12:00 МСК.</summary>
    public static readonly DateTimeOffset DefaultUtcNow = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public StubCbrHandler Cbr { get; } = new();

    /// <summary>
    /// Время для «сегодня по МСК» и истечения кэша. Глобальный TimeProvider не подменяется:
    /// его использует resilience-pipeline, и таймауты на фейковом времени никогда бы не сработали.
    /// </summary>
    public FakeTimeProvider Time { get; } = new(utcNow ?? DefaultUtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Cbr:BaseUrl", "https://cbr.test/");
        builder.UseSetting("Cbr:AttemptTimeout", "00:00:00.200");
        builder.UseSetting("Cbr:TotalTimeout", "00:00:01");
        builder.UseSetting("Cbr:MaxRetryAttempts", "2");
        builder.UseSetting("Cbr:RetryBaseDelay", "00:00:00");

        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new MoscowClock(Time));
            services.Configure<MemoryCacheOptions>(options => options.Clock = new TimeProviderSystemClock(Time));
            services.AddHttpClient(CbrRatesClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Cbr);
        });
    }
}
