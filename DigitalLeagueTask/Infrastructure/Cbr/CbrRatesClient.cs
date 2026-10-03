using System.Globalization;
using DigitalLeagueTask.Domain;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DigitalLeagueTask.Infrastructure.Cbr;

/// <summary>
/// Клиент <c>XML_daily.asp</c> (design.md, ADR-1). Повторы и таймауты выполняет
/// resilience-pipeline HttpClient; здесь — только перевод сбоев в исключения домена (ADR-5).
/// </summary>
internal sealed class CbrRatesClient(
    HttpClient httpClient,
    IOptions<CbrOptions> options,
    ILogger<CbrRatesClient> logger) : ICbrRatesClient
{
    public const string HttpClientName = nameof(CbrRatesClient);

    public async Task<DailyRates> GetDailyRatesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var requestUri = $"{options.Value.DailyRatesPath}?date_req={date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new CbrUnavailableException($"ЦБ ответил кодом {(int)response.StatusCode}.");
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var rates = await CbrXmlParser.ParseAsync(content, cancellationToken);

            logger.LogInformation(
                "Получены курсы ЦБ на {RequestedDate}: фактическая дата {RateDate}, валют {RatesCount}",
                date, rates.RateDate, rates.Rates.Count);

            return rates;
        }
        catch (TimeoutRejectedException ex)
        {
            throw new CbrTimeoutException("ЦБ не ответил за отведённое время.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            throw new CbrUnavailableException("Обращения к ЦБ временно приостановлены после серии сбоев.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CbrUnavailableException("Не удалось выполнить запрос к ЦБ.", ex);
        }
        // OperationCanceledException при отмене запроса клиентом пробрасывается как есть — это не сбой ЦБ.
    }
}
