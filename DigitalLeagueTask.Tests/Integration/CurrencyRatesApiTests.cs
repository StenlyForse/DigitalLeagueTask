using System.Net;
using System.Text.Json;
using DigitalLeagueTask.Tests.Support;

namespace DigitalLeagueTask.Tests.Integration;

/// <summary>Критерии приёмки из specs/spec.md через HTTP (T-11). Имя теста начинается с номера AC.</summary>
public sealed class CurrencyRatesApiTests : IDisposable
{
    private const string Endpoint = "/api/v1/currency-rates";

    private readonly CbrApiFactory _factory = new();
    private readonly HttpClient _client;

    public CurrencyRatesApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private StubCbrHandler Cbr => _factory.Cbr;

    // ---------- FR-1 ----------

    [Fact]
    public async Task AC_1_1_returns_all_currencies_for_date()
    {
        Cbr.ThenFile(CbrXml.Thursday20261001File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("2026-10-01", json.RootElement.GetProperty("requestedDate").GetString());
        Assert.Equal("2026-10-01", json.RootElement.GetProperty("rateDate").GetString());
        Assert.Equal(54, json.RootElement.GetProperty("rates").GetArrayLength());
    }

    // ---------- FR-2 ----------

    [Fact]
    public async Task AC_2_1_uses_today_in_moscow_time_when_date_is_not_passed()
    {
        // 2026-09-30 23:30 UTC = 2026-10-01 02:30 МСК
        using var factory = new CbrApiFactory(new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero));
        factory.Cbr.ThenFile(CbrXml.Thursday20261001File);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith("date_req=01/10/2026", Assert.Single(factory.Cbr.Requests).ToString());
        using var json = await ReadJsonAsync(response);
        Assert.Equal("2026-10-01", json.RootElement.GetProperty("requestedDate").GetString());
    }

    // ---------- FR-3 ----------

    [Theory]
    [InlineData("USD")] // AC-3.1
    [InlineData("usd")] // AC-3.2
    public async Task AC_3_1_and_3_2_returns_single_currency_by_code_case_insensitive(string code)
    {
        Cbr.ThenFile(CbrXml.Thursday20261001File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01&code={code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var rate = Assert.Single(json.RootElement.GetProperty("rates").EnumerateArray());
        Assert.Equal("USD", rate.GetProperty("charCode").GetString());
    }

    // ---------- FR-4 ----------

    [Fact]
    public async Task AC_4_1_returns_204_without_body_when_currency_is_absent()
    {
        Cbr.ThenFile(CbrXml.Thursday20261001File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01&code=XAU");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    // ---------- FR-5 ----------

    [Fact]
    public async Task AC_5_1_returns_last_effective_rate_with_actual_date_for_weekend()
    {
        Cbr.ThenFile(CbrXml.Sunday20260927File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-09-27");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("2026-09-27", json.RootElement.GetProperty("requestedDate").GetString());
        Assert.Equal("2026-09-26", json.RootElement.GetProperty("rateDate").GetString());
    }

    // ---------- FR-6 ----------

    [Fact]
    public async Task AC_6_1_returns_all_currency_fields_as_json_numbers()
    {
        Cbr.ThenFile(CbrXml.Thursday20261001File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01&code=JPY");

        using var json = await ReadJsonAsync(response);
        var jpy = Assert.Single(json.RootElement.GetProperty("rates").EnumerateArray());
        Assert.Equal("JPY", jpy.GetProperty("charCode").GetString());
        Assert.Equal("392", jpy.GetProperty("numCode").GetString());
        Assert.Equal("Иен", jpy.GetProperty("name").GetString());
        Assert.Equal("100", jpy.GetProperty("nominal").GetRawText());
        Assert.Equal("53.1038", jpy.GetProperty("value").GetRawText());
        Assert.Equal("0.531038", jpy.GetProperty("unitRate").GetRawText());
    }

    // ---------- FR-7 ----------

    [Theory]
    [InlineData("date=01.10.2026", "date")]          // AC-7.1
    [InlineData("date=2026-02-30", "date")]          // AC-7.2
    [InlineData("date=2026-10-03", "date")]          // AC-7.3: сегодня 2026-10-01
    [InlineData("date=1992-06-30", "date")]          // AC-7.4
    [InlineData("code=US", "code")]                  // AC-7.5
    [InlineData("code=USDD", "code")]                // AC-7.5
    [InlineData("code=U5D", "code")]                 // AC-7.5
    [InlineData("code=%D0%94%D0%9E%D0%9B", "code")]  // AC-7.5: «ДОЛ»
    public async Task AC_7_returns_400_problem_details_for_invalid_parameter(string query, string parameter)
    {
        using var response = await _client.GetAsync($"{Endpoint}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(parameter, out _));
        Assert.Equal(0, Cbr.CallCount);
    }

    [Fact]
    public async Task AC_7_3_accepts_tomorrow()
    {
        Cbr.ThenFile(CbrXml.Thursday20261001File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-02");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AC_7_4_accepts_first_publication_date()
    {
        Cbr.ThenXml(CbrXml.Daily("01.07.1992"));

        using var response = await _client.GetAsync($"{Endpoint}?date=1992-07-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AC_7_6_reports_errors_for_both_parameters()
    {
        using var response = await _client.GetAsync($"{Endpoint}?date=01.10.2026&code=US");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("date", out _));
        Assert.True(errors.TryGetProperty("code", out _));
    }

    // ---------- FR-8 ----------

    [Fact]
    public async Task AC_8_1_returns_502_when_cbr_fails_on_all_attempts()
    {
        Cbr.ThenStatus(HttpStatusCode.InternalServerError);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01");

        await AssertSourceProblemAsync(response, HttpStatusCode.BadGateway);
        Assert.Equal(3, Cbr.CallCount); // первая попытка + 2 повтора (NFR-2)
    }

    [Fact]
    public async Task AC_8_2_returns_502_when_cbr_returns_invalid_xml()
    {
        Cbr.ThenXml("<ValCurs>\nError in parameters\n</ValCurs>");

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01");

        await AssertSourceProblemAsync(response, HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task AC_8_3_returns_504_when_cbr_does_not_respond_in_time()
    {
        Cbr.ThenHang();

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01");

        await AssertSourceProblemAsync(response, HttpStatusCode.GatewayTimeout);
    }

    // ---------- NFR ----------

    [Fact]
    public async Task AC_N1_1_serves_repeated_past_date_requests_from_cache()
    {
        Cbr.ThenFile(CbrXml.Sunday20260927File);

        using var first = await _client.GetAsync($"{Endpoint}?date=2026-09-27");
        using var second = await _client.GetAsync($"{Endpoint}?date=2026-09-27&code=USD");
        using var third = await _client.GetAsync($"{Endpoint}?date=2026-09-27&code=XAU");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, third.StatusCode);
        Assert.Equal(1, Cbr.CallCount);
    }

    [Fact]
    public async Task AC_N1_2_requests_cbr_again_after_current_date_ttl_expires()
    {
        Cbr.ThenFile(CbrXml.Thursday20261001File);

        using var first = await _client.GetAsync($"{Endpoint}?date=2026-10-01");
        _factory.Time.Advance(TimeSpan.FromMinutes(16));
        using var second = await _client.GetAsync($"{Endpoint}?date=2026-10-01");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, Cbr.CallCount);
    }

    [Fact]
    public async Task AC_N2_1_retries_transient_cbr_failure()
    {
        Cbr.ThenStatus(HttpStatusCode.ServiceUnavailable).ThenFile(CbrXml.Thursday20261001File);

        using var response = await _client.GetAsync($"{Endpoint}?date=2026-10-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, Cbr.CallCount);
    }

    [Fact]
    public async Task AC_N4_1_health_check_does_not_depend_on_cbr()
    {
        Cbr.ThenStatus(HttpStatusCode.InternalServerError);

        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, Cbr.CallCount);
    }

    // ---------- helpers ----------

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    /// <summary>ProblemDetails об ошибке источника без внутренних деталей (FR-8, NFR-3).</summary>
    private static async Task AssertSourceProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal((int)expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain("cbr.test", body);
    }
}
