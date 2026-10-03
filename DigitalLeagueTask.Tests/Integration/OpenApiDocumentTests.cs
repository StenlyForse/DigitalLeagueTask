using System.Globalization;
using System.Text;
using DigitalLeagueTask.Api.OpenApi;
using DigitalLeagueTask.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;

namespace DigitalLeagueTask.Tests.Integration;

/// <summary>
/// Snapshot-проверка specs/openapi.yaml (D-1; design.md, ADR-10): файл в репозитории должен совпадать
/// с описанием, сгенерированным из кода. Обновить файл: <c>UPDATE_OPENAPI=1 dotnet test --filter OpenApiDocumentTests</c>.
/// </summary>
public sealed class OpenApiDocumentTests
{
    private const string UpdateVariable = "UPDATE_OPENAPI";

    [Fact]
    public void Committed_openapi_yaml_matches_document_generated_from_code()
    {
        var generated = GenerateYaml();
        var path = Path.Combine(FindRepositoryRoot(), "specs", "openapi.yaml");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, generated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(path), $"Нет файла {path}. Создайте его: {UpdateVariable}=1 dotnet test --filter {nameof(OpenApiDocumentTests)}");
        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.True(committed == generated,
            $"specs/openapi.yaml не соответствует коду ({FirstDifference(committed, generated)}). " +
            $"Обновите файл: {UpdateVariable}=1 dotnet test --filter {nameof(OpenApiDocumentTests)}");
    }

    [Fact]
    public void Document_is_openapi_3_0_and_describes_contract_from_design()
    {
        var yaml = GenerateYaml();

        Assert.StartsWith("openapi: 3.0.", yaml);
        Assert.Contains("/api/v1/currency-rates:", yaml);
        foreach (var statusCode in new[] { "200", "204", "400", "502", "504" })
        {
            Assert.Contains($"'{statusCode}':", yaml);
        }

        Assert.Contains("format: date", yaml);
        Assert.Contains("pattern: '^[A-Za-z]{3}$'", yaml);
        // Многострочный XML-комментарий даёт в YAML экранированный «\r\n» на Windows и «\n» на Linux:
        // файл зависел бы от ОС, на которой его сгенерировали. Комментарии пишем в одну строку.
        Assert.DoesNotContain(@"\r", yaml);
    }

    private static string GenerateYaml()
    {
        using var factory = new CbrApiFactory();
        var document = factory.Services.GetRequiredService<ISwaggerProvider>()
            .GetSwagger(OpenApiServiceCollectionExtensions.DocumentName);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        document.SerializeAsV3(new OpenApiYamlWriter(writer));
        return writer.ToString().ReplaceLineEndings("\n") + "\n";
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DigitalLeagueTask.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Не найден корень репозитория (DigitalLeagueTask.slnx).");
    }

    private static string FirstDifference(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var e = i < expectedLines.Length ? expectedLines[i] : "<нет строки>";
            var a = i < actualLines.Length ? actualLines[i] : "<нет строки>";
            if (e != a)
            {
                return $"строка {i + 1}: в файле «{e}», из кода «{a}»";
            }
        }

        return "различий в строках нет";
    }
}
