using DigitalLeagueTask.Infrastructure.Cbr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DigitalLeagueTask.Tests.Unit;

public class CbrServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_client_with_options_from_configuration()
    {
        // NFR-5: настройки берутся из конфигурации; resilience-pipeline с ними проходит валидацию.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbr:BaseUrl"] = "https://cbr.test/",
                ["Cbr:AttemptTimeout"] = "00:00:02",
                ["Cbr:TotalTimeout"] = "00:00:07",
                ["Cbr:MaxRetryAttempts"] = "3",
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddCbrRatesClient()
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CbrOptions>>().Value;
        Assert.Equal(new Uri("https://cbr.test/"), options.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(2), options.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(7), options.TotalTimeout);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.IsType<CbrRatesClient>(provider.GetRequiredService<ICbrRatesClient>());
    }

    [Fact]
    public void Rejects_invalid_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cbr:MaxRetryAttempts"] = "0" })
            .Build();

        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddCbrRatesClient()
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CbrOptions>>().Value);
    }
}
