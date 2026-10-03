using Microsoft.OpenApi.Models;

namespace DigitalLeagueTask.Api.OpenApi;

public static class OpenApiServiceCollectionExtensions
{
    public const string DocumentName = "v1";

    /// <summary>OpenAPI-описание, генерируемое из кода (D-1; design.md, ADR-10).</summary>
    public static IServiceCollection AddCurrencyRatesOpenApi(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Шлюз курсов валют Банка России",
                Version = "1.0",
                Description = "Официальные курсы иностранных валют к рублю, устанавливаемые Банком России (источник — cbr.ru).",
            });

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            options.IncludeXmlComments(xmlPath);

            options.OperationFilter<RatesQueryParametersFilter>();

            // Строки в DTO не допускают null — описание должно это отражать.
            options.SupportNonNullableReferenceTypes();
            options.NonNullableReferenceTypesAsRequired();

            // decimal — точное десятичное число; format: double ввёл бы клиентов в заблуждение о точности.
            options.MapType<decimal>(() => new OpenApiSchema { Type = "number" });
        });
}
