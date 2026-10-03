# Шлюз курсов валют ЦБ РФ (design.md, ADR-11).
# Сборка: docker build -t currency-gateway .
# Запуск: docker run --rm -p 8080:8080 currency-gateway

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Сначала только проект — слой с пакетами NuGet кэшируется, пока не меняются зависимости.
COPY DigitalLeagueTask/DigitalLeagueTask.csproj DigitalLeagueTask/
RUN dotnet restore DigitalLeagueTask/DigitalLeagueTask.csproj

COPY DigitalLeagueTask/ DigitalLeagueTask/
RUN dotnet publish DigitalLeagueTask/DigitalLeagueTask.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Образы .NET 8 по умолчанию слушают 8080; задано явно, чтобы не зависеть от базового образа.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Непривилегированный пользователь app, предусмотренный в образах .NET 8.
USER $APP_UID

# HEALTHCHECK не задан: в aspnet-образе нет curl/wget; проверку /health выполняет оркестратор.
ENTRYPOINT ["dotnet", "DigitalLeagueTask.dll"]
