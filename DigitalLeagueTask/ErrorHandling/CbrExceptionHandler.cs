using DigitalLeagueTask.Infrastructure.Cbr;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DigitalLeagueTask.ErrorHandling;

/// <summary>
/// Сбои источника данных → ProblemDetails 502/504 без внутренних деталей (FR-8, NFR-3; design.md, ADR-8).
/// Прочие исключения обрабатывает стандартный обработчик (500).
/// </summary>
internal sealed class CbrExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<CbrExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            CbrTimeoutException => (StatusCodes.Status504GatewayTimeout, "Источник данных Банка России не ответил вовремя."),
            CbrUnavailableException => (StatusCodes.Status502BadGateway, "Источник данных Банка России недоступен."),
            _ => (0, null),
        };

        if (title is null)
        {
            return false;
        }

        logger.LogWarning(exception, "Сбой источника данных ЦБ, ответ {StatusCode}: {Reason}", statusCode, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = statusCode, Title = title },
        });
    }
}
