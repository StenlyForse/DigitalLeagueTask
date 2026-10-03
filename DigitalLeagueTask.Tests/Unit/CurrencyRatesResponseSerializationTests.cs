using System.Text.Json;
using DigitalLeagueTask.Api.Contracts;
using DigitalLeagueTask.Domain;

namespace DigitalLeagueTask.Tests.Unit;

public class CurrencyRatesResponseSerializationTests
{
    // Те же настройки, что ASP.NET Core использует по умолчанию для JSON-ответов.
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serializes_dates_as_iso_numbers_as_json_numbers_and_names_in_camel_case()
    {
        // FR-5, FR-6, A-9
        var rates = new DailyRates(
            new DateOnly(2026, 9, 26),
            [new CurrencyRate("JPY", "392", "Иен", 100, 53.1351m, 0.531351m)]);

        var json = JsonSerializer.Serialize(CurrencyRatesResponse.From(new DateOnly(2026, 9, 27), rates), WebOptions);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("2026-09-27", root.GetProperty("requestedDate").GetString());
        Assert.Equal("2026-09-26", root.GetProperty("rateDate").GetString());

        var rate = Assert.Single(root.GetProperty("rates").EnumerateArray());
        Assert.Equal("JPY", rate.GetProperty("charCode").GetString());
        Assert.Equal("392", rate.GetProperty("numCode").GetString());
        Assert.Equal("Иен", rate.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Number, rate.GetProperty("nominal").ValueKind);
        Assert.Equal("100", rate.GetProperty("nominal").GetRawText());
        Assert.Equal(JsonValueKind.Number, rate.GetProperty("value").ValueKind);
        Assert.Equal("53.1351", rate.GetProperty("value").GetRawText());
        Assert.Equal("0.531351", rate.GetProperty("unitRate").GetRawText());
    }
}
