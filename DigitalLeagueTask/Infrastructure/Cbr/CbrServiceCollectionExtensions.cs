using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace DigitalLeagueTask.Infrastructure.Cbr;

public static class CbrServiceCollectionExtensions
{
    /// <summary>Регистрирует клиент ЦБ: настройки, typed HttpClient и стандартный resilience-pipeline.</summary>
    public static IServiceCollection AddCbrRatesClient(this IServiceCollection services)
    {
        services.AddOptions<CbrOptions>()
            .BindConfiguration(CbrOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<ICbrRatesClient, CbrRatesClient>(CbrRatesClient.HttpClientName, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<CbrOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                // Таймаутами управляет resilience-pipeline.
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DigitalLeagueTask-CurrencyGateway/1.0");
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<CbrOptions>>().Value;
                resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = options.TotalTimeout;
                resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
                resilience.Retry.Delay = options.RetryBaseDelay;
                // Окно выборки circuit breaker должно быть не меньше двух таймаутов попытки.
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromTicks(
                    Math.Max(resilience.CircuitBreaker.SamplingDuration.Ticks, options.AttemptTimeout.Ticks * 2));
            });

        return services;
    }
}
