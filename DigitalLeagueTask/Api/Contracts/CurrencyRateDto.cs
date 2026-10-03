using DigitalLeagueTask.Domain;

namespace DigitalLeagueTask.Api.Contracts;

/// <summary>Курс одной валюты.</summary>
/// <param name="CharCode">Буквенный код ISO 4217.</param>
/// <param name="NumCode">Цифровой код ISO 4217.</param>
/// <param name="Name">Наименование валюты.</param>
/// <param name="Nominal">Количество единиц валюты, за которое установлен курс.</param>
/// <param name="Value">Курс за номинал, руб.</param>
/// <param name="UnitRate">Курс за одну единицу валюты, руб.</param>
public sealed record CurrencyRateDto(
    string CharCode,
    string NumCode,
    string Name,
    int Nominal,
    decimal Value,
    decimal UnitRate)
{
    public static CurrencyRateDto From(CurrencyRate rate) =>
        new(rate.CharCode, rate.NumCode, rate.Name, rate.Nominal, rate.Value, rate.UnitRate);
}
