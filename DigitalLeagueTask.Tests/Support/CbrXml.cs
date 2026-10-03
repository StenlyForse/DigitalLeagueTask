using System.Text;

namespace DigitalLeagueTask.Tests.Support;

/// <summary>Построение ответов ЦБ (<c>XML_daily.asp</c>) для тестов.</summary>
internal static class CbrXml
{
    static CbrXml()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding Windows1251 => Encoding.GetEncoding(1251);

    /// <summary>Реальный ответ ЦБ на запрос 01.10.2026 (четверг): фактическая дата 01.10.2026, 54 валюты.</summary>
    public const string Thursday20261001File = "cbr_daily_req_2026-10-01.xml";

    /// <summary>Реальный ответ ЦБ на запрос 27.09.2026 (воскресенье): фактическая дата 26.09.2026, 54 валюты.</summary>
    public const string Sunday20260927File = "cbr_daily_req_2026-09-27.xml";

    public static byte[] ReadFile(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", fileName));

    public static string Daily(string date, params string[] valutes) =>
        $"""<?xml version="1.0" encoding="windows-1251"?><ValCurs Date="{date}" name="Foreign Currency Market">{string.Concat(valutes)}</ValCurs>""";

    public static string Valute(
        string charCode,
        string numCode = "840",
        string name = "Доллар США",
        string nominal = "1",
        string value = "91,8700",
        string? vunitRate = "91,87")
    {
        var vunitRateElement = vunitRate is null ? "" : $"<VunitRate>{vunitRate}</VunitRate>";
        return $"<Valute ID=\"R00000\"><NumCode>{numCode}</NumCode><CharCode>{charCode}</CharCode>" +
               $"<Nominal>{nominal}</Nominal><Name>{name}</Name><Value>{value}</Value>{vunitRateElement}</Valute>";
    }

    public static byte[] ToBytes(string xml) => Windows1251.GetBytes(xml);

    public static Stream ToStream(string xml) => new MemoryStream(ToBytes(xml));
}
