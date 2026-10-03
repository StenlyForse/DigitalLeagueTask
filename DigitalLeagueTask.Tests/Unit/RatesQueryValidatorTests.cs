using DigitalLeagueTask.Api;

namespace DigitalLeagueTask.Tests.Unit;

public class RatesQueryValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Fact]
    public void Uses_today_when_date_is_not_passed()
    {
        // FR-2
        var result = RatesQueryValidator.Validate(null, null, Today);

        Assert.True(result.IsValid);
        Assert.Equal(new RatesQuery(Today, null), result.Query);
    }

    [Fact]
    public void Accepts_date_and_normalizes_code_to_upper_case()
    {
        // FR-3, AC-3.2
        var result = RatesQueryValidator.Validate("2026-09-27", "usd", Today);

        Assert.Equal(new RatesQuery(new DateOnly(2026, 9, 27), "USD"), result.Query);
    }

    [Theory]
    [InlineData("01.10.2026")] // AC-7.1
    [InlineData("2026-02-30")] // AC-7.2
    [InlineData("2026-1-1")]
    [InlineData("20261001")]
    [InlineData("2026-10-01T00:00:00")]
    public void Rejects_date_in_wrong_format(string date)
    {
        var result = RatesQueryValidator.Validate(date, null, Today);

        Assert.False(result.IsValid);
        Assert.Equal(["Дата должна быть в формате yyyy-MM-dd."], result.Errors["date"]);
    }

    [Fact]
    public void Rejects_date_after_tomorrow_and_accepts_tomorrow()
    {
        // AC-7.3
        var afterTomorrow = RatesQueryValidator.Validate("2026-10-03", null, Today);
        var tomorrow = RatesQueryValidator.Validate("2026-10-02", null, Today);

        Assert.Equal(["Дата не может быть позже 2026-10-02."], afterTomorrow.Errors["date"]);
        Assert.True(tomorrow.IsValid);
    }

    [Fact]
    public void Rejects_date_before_first_cbr_publication_and_accepts_boundary()
    {
        // AC-7.4
        var beforeMin = RatesQueryValidator.Validate("1992-06-30", null, Today);
        var min = RatesQueryValidator.Validate("1992-07-01", null, Today);

        Assert.Equal(["Дата не может быть раньше 1992-07-01."], beforeMin.Errors["date"]);
        Assert.True(min.IsValid);
    }

    [Theory]
    // AC-7.5
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U5D")]
    [InlineData("ДОЛ")]
    [InlineData(" US")]
    public void Rejects_code_that_is_not_three_latin_letters(string code)
    {
        var result = RatesQueryValidator.Validate(null, code, Today);

        Assert.False(result.IsValid);
        Assert.Equal(["Код валюты должен состоять из трёх латинских букв (ISO 4217)."], result.Errors["code"]);
    }

    [Fact]
    public void Reports_errors_for_both_parameters_at_once()
    {
        // AC-7.6
        var result = RatesQueryValidator.Validate("01.10.2026", "US", Today);

        Assert.False(result.IsValid);
        Assert.Equal(["code", "date"], result.Errors.Keys.Order());
    }
}
