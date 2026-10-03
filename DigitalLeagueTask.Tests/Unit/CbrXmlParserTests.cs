using DigitalLeagueTask.Infrastructure.Cbr;
using DigitalLeagueTask.Tests.Support;

namespace DigitalLeagueTask.Tests.Unit;

public class CbrXmlParserTests
{
    [Fact]
    public async Task Parses_real_cbr_response_for_working_day()
    {
        // FR-1, AC-1.1
        using var stream = new MemoryStream(CbrXml.ReadFile(CbrXml.Thursday20261001File));

        var result = await CbrXmlParser.ParseAsync(stream, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 1), result.RateDate);
        Assert.Equal(54, result.Rates.Count);
    }

    [Fact]
    public async Task Parses_real_cbr_response_with_actual_rate_date()
    {
        // FR-5, AC-5.1: на запрос воскресенья 27.09.2026 ЦБ вернул курсы с фактической датой 26.09.2026.
        using var stream = new MemoryStream(CbrXml.ReadFile(CbrXml.Sunday20260927File));

        var result = await CbrXmlParser.ParseAsync(stream, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 26), result.RateDate);
    }

    [Fact]
    public async Task Parses_all_currency_fields_including_nominal_and_cyrillic_name()
    {
        // FR-6, AC-6.1
        using var stream = new MemoryStream(CbrXml.ReadFile(CbrXml.Thursday20261001File));

        var result = await CbrXmlParser.ParseAsync(stream, CancellationToken.None);

        var jpy = Assert.Single(result.Rates, r => r.CharCode == "JPY");
        Assert.Equal("392", jpy.NumCode);
        Assert.Equal("Иен", jpy.Name);
        Assert.Equal(100, jpy.Nominal);
        Assert.Equal(53.1038m, jpy.Value);
        Assert.Equal(0.531038m, jpy.UnitRate);
    }

    [Fact]
    public async Task Keeps_leading_zeros_in_numeric_code()
    {
        using var stream = new MemoryStream(CbrXml.ReadFile(CbrXml.Thursday20261001File));

        var result = await CbrXmlParser.ParseAsync(stream, CancellationToken.None);

        Assert.Equal("036", Assert.Single(result.Rates, r => r.CharCode == "AUD").NumCode);
    }

    [Fact]
    public async Task Parses_unit_rate_in_exponential_notation()
    {
        // В реальном ответе на 01.10.2026: IRR, номинал 1000000, VunitRate = 4,8189E-05.
        using var stream = new MemoryStream(CbrXml.ReadFile(CbrXml.Thursday20261001File));

        var result = await CbrXmlParser.ParseAsync(stream, CancellationToken.None);

        Assert.Equal(0.000048189m, Assert.Single(result.Rates, r => r.CharCode == "IRR").UnitRate);
    }

    [Fact]
    public async Task Calculates_unit_rate_when_VunitRate_is_absent()
    {
        var xml = CbrXml.Daily("10.03.1997",
            CbrXml.Valute("JPY", "392", "Японских иен", nominal: "100", value: "4703,5000", vunitRate: null));

        var result = await CbrXmlParser.ParseAsync(CbrXml.ToStream(xml), CancellationToken.None);

        Assert.Equal(47.035m, Assert.Single(result.Rates).UnitRate);
    }

    [Fact]
    public async Task Returns_empty_list_when_source_has_no_currencies()
    {
        // A-8
        var result = await CbrXmlParser.ParseAsync(CbrXml.ToStream(CbrXml.Daily("01.07.1992")), CancellationToken.None);

        Assert.Equal(new DateOnly(1992, 7, 1), result.RateDate);
        Assert.Empty(result.Rates);
    }

    public static TheoryData<string> InvalidResponses => new()
    {
        "это не xml",
        "<?xml version=\"1.0\" encoding=\"windows-1251\"?><ValCurs>\nError in parameters\n</ValCurs>",
        "<?xml version=\"1.0\" encoding=\"windows-1251\"?><Other Date=\"01.10.2026\" />",
        CbrXml.Daily("2026-10-01"),
        CbrXml.Daily("01.10.2026", CbrXml.Valute("USD", value: "n/a")),
        CbrXml.Daily("01.10.2026", CbrXml.Valute("USD", nominal: "0")),
        CbrXml.Daily("01.10.2026", CbrXml.Valute("")),
    };

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public async Task Throws_CbrUnavailableException_for_invalid_response(string xml)
    {
        // FR-8, AC-8.2
        await Assert.ThrowsAsync<CbrUnavailableException>(
            () => CbrXmlParser.ParseAsync(CbrXml.ToStream(xml), CancellationToken.None));
    }
}
