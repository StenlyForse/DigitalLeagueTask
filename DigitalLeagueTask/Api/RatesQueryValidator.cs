using System.Globalization;
using System.Text.RegularExpressions;

namespace DigitalLeagueTask.Api;

/// <summary>Проверенные параметры запроса курсов.</summary>
/// <param name="Date">Дата курса (запрошенная или текущая по МСК).</param>
/// <param name="Code">Буквенный код валюты в верхнем регистре; <c>null</c> — все валюты.</param>
public sealed record RatesQuery(DateOnly Date, string? Code);

/// <summary>Результат проверки: либо параметры, либо ошибки по именам query-параметров.</summary>
public sealed record RatesQueryValidationResult(RatesQuery? Query, Dictionary<string, string[]> Errors)
{
    public bool IsValid => Query is not null;
}

/// <summary>Проверка параметров запроса курсов (FR-2, FR-7; design.md, ADR-7).</summary>
public static partial class RatesQueryValidator
{
    public const string DateFormat = "yyyy-MM-dd";

    public const string CurrencyCodePattern = "^[A-Za-z]{3}$";

    /// <summary>С этой даты ЦБ публикует курсы в <c>XML_daily.asp</c> (A-6).</summary>
    public static readonly DateOnly MinDate = new(1992, 7, 1);

    public static RatesQueryValidationResult Validate(string? date, string? code, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();

        var requestedDate = today;
        if (!string.IsNullOrEmpty(date))
        {
            var dateError = ValidateDate(date, today, out requestedDate);
            if (dateError is not null)
            {
                errors["date"] = [dateError];
            }
        }

        if (!string.IsNullOrEmpty(code) && !CurrencyCodeRegex().IsMatch(code))
        {
            errors["code"] = ["Код валюты должен состоять из трёх латинских букв (ISO 4217)."];
        }

        if (errors.Count > 0)
        {
            return new RatesQueryValidationResult(null, errors);
        }

        var normalizedCode = string.IsNullOrEmpty(code) ? null : code.ToUpperInvariant();
        return new RatesQueryValidationResult(new RatesQuery(requestedDate, normalizedCode), errors);
    }

    private static string? ValidateDate(string date, DateOnly today, out DateOnly requestedDate)
    {
        if (!DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out requestedDate))
        {
            return $"Дата должна быть в формате {DateFormat}.";
        }

        var maxDate = today.AddDays(1);
        if (requestedDate > maxDate)
        {
            return $"Дата не может быть позже {maxDate.ToString(DateFormat, CultureInfo.InvariantCulture)}.";
        }

        if (requestedDate < MinDate)
        {
            return $"Дата не может быть раньше {MinDate.ToString(DateFormat, CultureInfo.InvariantCulture)}.";
        }

        return null;
    }

    [GeneratedRegex(CurrencyCodePattern, RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyCodeRegex();
}
