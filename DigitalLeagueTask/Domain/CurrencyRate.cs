namespace DigitalLeagueTask.Domain;

/// <summary>Официальный курс одной валюты к рублю, установленный Банком России.</summary>
/// <param name="CharCode">Буквенный код ISO 4217.</param>
/// <param name="NumCode">Цифровой код ISO 4217 (строка — с ведущими нулями).</param>
/// <param name="Name">Наименование валюты в источнике.</param>
/// <param name="Nominal">Количество единиц валюты, за которое установлен курс.</param>
/// <param name="Value">Курс за номинал, руб.</param>
/// <param name="UnitRate">Курс за одну единицу валюты, руб.</param>
public sealed record CurrencyRate(
    string CharCode,
    string NumCode,
    string Name,
    int Nominal,
    decimal Value,
    decimal UnitRate);
