using System.Net;
using DigitalLeagueTask.Tests.Support;

namespace DigitalLeagueTask.Tests.Integration;

public sealed class SmokeTests : IDisposable
{
    private readonly CbrApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Application_starts_and_uses_cbr_stub_instead_of_network()
    {
        _factory.Cbr.ThenFile(CbrXml.Thursday20261001File);
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/currency-rates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://cbr.test/scripts/XML_daily.asp?date_req=01/10/2026", Assert.Single(_factory.Cbr.Requests).ToString());
    }
}
