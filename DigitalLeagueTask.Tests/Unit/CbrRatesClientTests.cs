using System.Net;
using DigitalLeagueTask.Infrastructure.Cbr;
using DigitalLeagueTask.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DigitalLeagueTask.Tests.Unit;

/// <summary>
/// Клиент без resilience-pipeline: формат запроса и перевод ошибок.
/// Повторы и таймауты проверяются интеграционными тестами (T-11).
/// </summary>
public class CbrRatesClientTests
{
    private readonly StubCbrHandler _handler = new();

    private CbrRatesClient CreateClient() => new(
        new HttpClient(_handler) { BaseAddress = new Uri("https://cbr.test/") },
        Options.Create(new CbrOptions()),
        NullLogger<CbrRatesClient>.Instance);

    [Fact]
    public async Task Requests_daily_rates_with_cbr_date_format()
    {
        _handler.ThenFile(CbrXml.Thursday20261001File);

        var rates = await CreateClient().GetDailyRatesAsync(new DateOnly(2026, 10, 1), CancellationToken.None);

        Assert.Equal("https://cbr.test/scripts/XML_daily.asp?date_req=01/10/2026", Assert.Single(_handler.Requests).ToString());
        Assert.Equal(new DateOnly(2026, 10, 1), rates.RateDate);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Throws_CbrUnavailableException_for_unsuccessful_status(HttpStatusCode statusCode)
    {
        _handler.ThenStatus(statusCode);

        await Assert.ThrowsAsync<CbrUnavailableException>(
            () => CreateClient().GetDailyRatesAsync(new DateOnly(2026, 10, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Throws_CbrUnavailableException_for_network_error()
    {
        _handler.ThenThrow(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<CbrUnavailableException>(
            () => CreateClient().GetDailyRatesAsync(new DateOnly(2026, 10, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Propagates_cancellation_by_caller()
    {
        _handler.ThenHang();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient().GetDailyRatesAsync(new DateOnly(2026, 10, 1), cts.Token));
    }
}
