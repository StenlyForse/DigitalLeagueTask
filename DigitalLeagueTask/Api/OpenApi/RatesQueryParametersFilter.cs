using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DigitalLeagueTask.Api.OpenApi;

/// <summary>
/// Форматы параметров запроса курсов в OpenAPI. Параметры принимаются строками и проверяются
/// <see cref="RatesQueryValidator"/> (ADR-7), поэтому формат берётся из его констант, а не из типов.
/// </summary>
internal sealed class RatesQueryParametersFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CurrencyRatesController))
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            switch (parameter.Name)
            {
                case "date":
                    parameter.Schema.Format = "date"; // RFC 3339 full-date = yyyy-MM-dd
                    parameter.Example = new OpenApiString("2026-10-01");
                    break;
                case "code":
                    parameter.Schema.Pattern = RatesQueryValidator.CurrencyCodePattern;
                    parameter.Example = new OpenApiString("USD");
                    break;
            }
        }
    }
}
