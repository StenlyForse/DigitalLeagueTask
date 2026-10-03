using DigitalLeagueTask.Api.Contracts;
using DigitalLeagueTask.Application;
using Microsoft.AspNetCore.Mvc;

namespace DigitalLeagueTask.Api;

/// <summary>Официальные курсы валют Банка России.</summary>
[ApiController]
[Route("api/v1/currency-rates")]
public sealed class CurrencyRatesController(ICurrencyRatesService ratesService, MoscowClock clock) : ControllerBase
{
    /// <summary>Курсы валют на дату.</summary>
    /// <param name="date">Дата курса в формате yyyy-MM-dd; по умолчанию — сегодня по московскому времени.</param>
    /// <param name="code">Буквенный код валюты ISO 4217 (регистр не важен); по умолчанию — все валюты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <remarks>Если на дату ЦБ курс не устанавливал (выходной, праздник), возвращается последний действующий курс; дата его установления — в поле <c>rateDate</c>.</remarks>
    /// <response code="200">Курсы найдены.</response>
    /// <response code="204">Валюты с указанным кодом нет в данных ЦБ на дату.</response>
    /// <response code="400">Некорректные параметры запроса.</response>
    /// <response code="502">Источник данных ЦБ недоступен или вернул некорректные данные.</response>
    /// <response code="504">Источник данных ЦБ не ответил за отведённое время.</response>
    [HttpGet]
    [ProducesResponseType<CurrencyRatesResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout, "application/problem+json")]
    public async Task<IActionResult> Get(
        [FromQuery] string? date,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        var validation = RatesQueryValidator.Validate(date, code, clock.Today);
        if (validation.Query is not { } query)
        {
            foreach (var (parameter, messages) in validation.Errors)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(parameter, message);
                }
            }

            // Через ProblemDetailsFactory: type, title и traceId заполняются единообразно.
            return ValidationProblem();
        }

        var rates = await ratesService.GetRatesAsync(query.Date, query.Code, cancellationToken);

        return rates is null
            ? NoContent()
            : Ok(CurrencyRatesResponse.From(query.Date, rates));
    }
}
