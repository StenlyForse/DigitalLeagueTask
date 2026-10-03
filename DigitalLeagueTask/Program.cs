using System.Diagnostics;
using DigitalLeagueTask.Api.OpenApi;
using DigitalLeagueTask.Application;
using DigitalLeagueTask.ErrorHandling;
using DigitalLeagueTask.Infrastructure.Cbr;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    // Структурированные логи в stdout для сбора в контейнерной среде (NFR-4).
    builder.Logging.ClearProviders().AddJsonConsole();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCurrencyRatesOpenApi();

builder.Services.AddProblemDetails(options =>
{
    // traceId во всех ProblemDetails, а не только в ответах MVC (NFR-4).
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<CbrExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MoscowClock>();
builder.Services.AddMemoryCache();
builder.Services.AddOptions<RatesCacheOptions>()
    .BindConfiguration(RatesCacheOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<ICurrencyRatesService, CurrencyRatesService>();
builder.Services.AddCbrRatesClient();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Точка входа; объявлена публичной для WebApplicationFactory в интеграционных тестах.</summary>
public partial class Program;
