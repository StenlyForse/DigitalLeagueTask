using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DigitalLeagueTask.Domain;

namespace DigitalLeagueTask.Infrastructure.Cbr;

/// <summary>
/// Разбор ответа <c>XML_daily.asp</c> Банка России.
/// Особенности источника: кодировка windows-1251, десятичная запятая,
/// элемент <c>VunitRate</c> присутствует не во всех исторических ответах.
/// </summary>
internal static class CbrXmlParser
{
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "dd/MM/yyyy"];

    private static readonly NumberFormatInfo CbrNumberFormat = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
    };

    static CbrXmlParser()
    {
        // windows-1251 не входит в набор кодировок .NET по умолчанию.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <exception cref="CbrUnavailableException">Ответ не является корректным документом ЦБ.</exception>
    public static async Task<DailyRates> ParseAsync(Stream content, CancellationToken cancellationToken)
    {
        XDocument document;
        try
        {
            document = await XDocument.LoadAsync(content, LoadOptions.None, cancellationToken);
        }
        catch (XmlException ex)
        {
            throw new CbrUnavailableException("Ответ ЦБ не является корректным XML.", ex);
        }

        var root = document.Root;
        if (root is null || root.Name.LocalName != "ValCurs")
        {
            throw new CbrUnavailableException("Ответ ЦБ не содержит элемента ValCurs.");
        }

        var rateDate = ParseDate(root.Attribute("Date")?.Value);
        var rates = root.Elements("Valute").Select(ParseRate).ToList();

        return new DailyRates(rateDate, rates);
    }

    private static DateOnly ParseDate(string? value)
    {
        if (value is null)
        {
            // Так ЦБ отвечает на некорректные параметры: <ValCurs>Error in parameters</ValCurs>.
            throw new CbrUnavailableException("Ответ ЦБ не содержит даты курса.");
        }

        return DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new CbrUnavailableException($"Некорректная дата курса в ответе ЦБ: '{value}'.");
    }

    private static CurrencyRate ParseRate(XElement valute)
    {
        var charCode = RequiredValue(valute, "CharCode");
        var numCode = RequiredValue(valute, "NumCode");
        var name = RequiredValue(valute, "Name");
        var nominal = ParseNominal(charCode, RequiredValue(valute, "Nominal"));
        var value = ParseDecimal(charCode, "Value", RequiredValue(valute, "Value"));

        var unitRateText = valute.Element("VunitRate")?.Value;
        var unitRate = unitRateText is null
            ? value / nominal // номиналы ЦБ — степени 10, деление точное
            : ParseDecimal(charCode, "VunitRate", unitRateText);

        return new CurrencyRate(charCode, numCode, name, nominal, value, unitRate);
    }

    private static string RequiredValue(XElement valute, string elementName)
    {
        var value = valute.Element(elementName)?.Value.Trim();
        return string.IsNullOrEmpty(value)
            ? throw new CbrUnavailableException($"В ответе ЦБ у валюты отсутствует элемент {elementName}.")
            : value;
    }

    private static int ParseNominal(string charCode, string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var nominal) && nominal > 0
            ? nominal
            : throw new CbrUnavailableException($"Некорректный номинал валюты {charCode} в ответе ЦБ: '{text}'.");

    // Мелкие курсы ЦБ передаёт в экспоненциальной записи: <VunitRate>4,8189E-05</VunitRate> (IRR).
    private const NumberStyles CbrNumberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private static decimal ParseDecimal(string charCode, string elementName, string text) =>
        decimal.TryParse(text, CbrNumberStyles, CbrNumberFormat, out var value)
            ? value
            : throw new CbrUnavailableException($"Некорректное значение {elementName} валюты {charCode} в ответе ЦБ: '{text}'.");
}
