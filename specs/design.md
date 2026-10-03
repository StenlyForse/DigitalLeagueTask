# Технический дизайн: шлюз курсов валют Банка России

| Поле | Значение |
|---|---|
| Версия | 1.0 |
| Дата | 2026-10-03 |
| Статус | Согласовано (Этап 3) |
| Спецификация | `specs/spec.md` v1.0 |

## 1. Обзор

Сервис — stateless ASP.NET Core Web API (.NET 8), один экземпляр. На каждый запрос он определяет дату, берёт курсы на эту дату из кэша в памяти или с сайта ЦБ, фильтрует по коду валюты и возвращает JSON.

```
Внутренняя система
      │  GET /api/v1/currency-rates?date=&code=
      ▼
┌─────────────────────────────────────────────────────────────┐
│ CurrencyRatesController                                     │
│   ├─ RatesQueryValidator   (FR-7)                           │
│   └─ MoscowClock           (FR-2)                           │
│              │                                              │
│              ▼                                              │
│ CurrencyRatesService  ── IMemoryCache (NFR-1)               │
│              │                                              │
│              ▼                                              │
│ CbrRatesClient (typed HttpClient)                           │
│   ├─ Standard resilience handler: retry/timeout (NFR-2)     │
│   └─ CbrXmlParser                                           │
│                                                             │
│ CbrExceptionHandler (IExceptionHandler) → 502/504 (FR-8)    │
│ /health (NFR-4)                                             │
└──────────────┬──────────────────────────────────────────────┘
               │ HTTPS GET scripts/XML_daily.asp?date_req=dd/MM/yyyy
               ▼
          www.cbr.ru
```

## 2. Структура решения

Один веб-проект с разделением на папки по слоям + один тестовый проект (см. ADR-2).

```
DigitalLeagueTask.slnx
specs/
  spec.md, design.md, tasks.md
DigitalLeagueTask/                          # веб-проект
  Program.cs                                # композиция: DI, middleware, endpoints
  appsettings.json
  Api/
    CurrencyRatesController.cs              # HTTP-контракт
    RatesQueryValidator.cs                  # FR-7
    Contracts/
      CurrencyRatesResponse.cs              # DTO ответа
      CurrencyRateDto.cs
  Application/
    ICurrencyRatesService.cs
    CurrencyRatesService.cs                 # кэш + фильтрация (FR-1, FR-3, FR-4, NFR-1)
    MoscowClock.cs                          # "сегодня" по МСК (FR-2)
    RatesCacheOptions.cs
  Domain/
    CurrencyRate.cs                         # record
    DailyRates.cs                           # record: фактическая дата + курсы
  Infrastructure/
    Cbr/
      ICbrRatesClient.cs
      CbrRatesClient.cs                     # HTTP к ЦБ, маппинг ошибок
      CbrXmlParser.cs                       # XML → DailyRates (FR-5, FR-6)
      CbrOptions.cs
      CbrUnavailableException.cs            # → 502
      CbrTimeoutException.cs                # → 504
  ErrorHandling/
    CbrExceptionHandler.cs                  # исключения → ProblemDetails (FR-8)
DigitalLeagueTask.Tests/                    # xUnit
  Unit/          # парсер, валидатор, MoscowClock
  Integration/   # WebApplicationFactory + заглушка ЦБ
  TestData/      # образцы XML ЦБ
```

Шаблонные `WeatherForecast*` удаляются.

## 3. Контракт API

### 3.1. `GET /api/v1/currency-rates`

| Параметр (query) | Тип | Обязательный | Описание |
|---|---|---|---|
| `date` | string `yyyy-MM-dd` | нет | Дата курса. По умолчанию — сегодня по МСК (FR-2). Допустимый диапазон: `1992-07-01` … завтра по МСК (FR-7). |
| `code` | string `[A-Za-z]{3}` | нет | Буквенный код ISO 4217, регистр не важен (FR-3). |

`date` принимается как строка и разбирается строго через `DateOnly.TryParseExact(..., "yyyy-MM-dd", CultureInfo.InvariantCulture)`. Стандартная привязка `DateOnly` не подходит: она зависит от культуры и принимает, например, `01.10.2026`.

### 3.2. Ответы

| Код | Когда | Тело | Требование |
|---|---|---|---|
| 200 | Курсы найдены (или код не передан) | `CurrencyRatesResponse` | FR-1, FR-3, FR-5, FR-6 |
| 204 | Код передан, валюты нет в данных на дату | пусто | FR-4 |
| 400 | Некорректные параметры | `ValidationProblemDetails` | FR-7 |
| 502 | ЦБ недоступен / ошибка / некорректный ответ | `ProblemDetails` | FR-8 |
| 504 | ЦБ не ответил за отведённое время | `ProblemDetails` | FR-8 |
| 500 | Непредвиденная ошибка | `ProblemDetails` | NFR-3 |

### 3.3. Модель ответа 200

```json
{
  "requestedDate": "2026-09-27",
  "rateDate": "2026-09-26",
  "rates": [
    {
      "charCode": "JPY",
      "numCode": "392",
      "name": "Иен",
      "nominal": 100,
      "value": 53.1351,
      "unitRate": 0.531351
    }
  ]
}
```

| Поле | Тип C# | JSON | Источник в XML ЦБ |
|---|---|---|---|
| `requestedDate` | `DateOnly` | string `yyyy-MM-dd` | параметр запроса / по умолчанию |
| `rateDate` | `DateOnly` | string `yyyy-MM-dd` | `ValCurs/@Date` (`dd.MM.yyyy`) |
| `charCode` | `string` | string | `CharCode` |
| `numCode` | `string` | string | `NumCode` (строка, чтобы сохранить ведущие нули: `036`) |
| `name` | `string` | string | `Name` |
| `nominal` | `int` | number | `Nominal` |
| `value` | `decimal` | number | `Value` (десятичная запятая) |
| `unitRate` | `decimal` | number | `VunitRate`; если элемента нет (старые даты) — `Value / Nominal` |

`DateOnly` сериализуется System.Text.Json в .NET 8 как `yyyy-MM-dd` из коробки; `decimal` — числом без потери точности. Имена — camelCase (поведение ASP.NET Core по умолчанию, A-9).

### 3.4. Формат ошибок

RFC 9457 через `AddProblemDetails()`; `traceId` добавляется во все ответы об ошибках через `CustomizeProblemDetails` (в .NET 8 его автоматически заполняет только MVC-фабрика).

400 (FR-7, AC-7.6 — несколько ошибок сразу):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "date": ["Дата должна быть в формате yyyy-MM-dd."],
    "code": ["Код валюты должен состоять из трёх латинских букв (ISO 4217)."]
  },
  "traceId": "00-…"
}
```

502 / 504 (FR-8):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.3",
  "title": "Источник данных Банка России недоступен.",
  "status": 502,
  "traceId": "00-…"
}
```

### 3.5. Служебные эндпоинты

| Эндпоинт | Назначение |
|---|---|
| `GET /health` | Liveness: 200 `Healthy`. Не проверяет ЦБ (AC-N4.1) — недоступность внешнего источника не должна приводить к перезапуску экземпляра. |
| `GET /swagger` | Swagger UI, только в Development. |

## 4. Модель данных и хранилище

### 4.1. Доменная модель

```csharp
public sealed record CurrencyRate(
    string CharCode, string NumCode, string Name,
    int Nominal, decimal Value, decimal UnitRate);

public sealed record DailyRates(DateOnly RateDate, IReadOnlyList<CurrencyRate> Rates);
```

### 4.2. Хранилище

Персистентного хранилища нет (out of scope). Единственное состояние — кэш `IMemoryCache` (ADR-4):

| Параметр | Значение |
|---|---|
| Ключ | `cbr:daily:{requestedDate:yyyy-MM-dd}` |
| Значение | `DailyRates` (все валюты на дату; фильтрация по коду — после кэша) |
| TTL, `requestedDate < сегодня МСК` | `RatesCache:PastDatesTtl`, по умолчанию 24 ч |
| TTL, `requestedDate ≥ сегодня МСК` | `RatesCache:CurrentDatesTtl`, по умолчанию 15 мин |
| Что не кэшируется | ошибки ЦБ |

Объём: ~50 валют × несколько сотен байт ≈ 10–20 КБ на дату — ограничение размера кэша для текущего масштаба не требуется.

### 4.3. Источник: `XML_daily.asp`

Запрос: `GET https://www.cbr.ru/scripts/XML_daily.asp?date_req=27/09/2026`

Ответ (кодировка `windows-1251`):
```xml
<ValCurs Date="26.09.2026" name="Foreign Currency Market">
  <Valute ID="R01820">
    <NumCode>392</NumCode><CharCode>JPY</CharCode><Nominal>100</Nominal>
    <Name>Иен</Name><Value>53,1351</Value><VunitRate>0,531351</VunitRate>
  </Valute>
</ValCurs>
```

Особенности, учитываемые в `CbrXmlParser`:
- `ValCurs/@Date` — фактическая дата курса, может отличаться от запрошенной (FR-5).
- Кодировка `windows-1251` → регистрируется `CodePagesEncodingProvider`; документ читается из потока, кодировка берётся из XML-декларации.
- Десятичный разделитель — запятая → разбор с `NumberFormatInfo { NumberDecimalSeparator = "," }`.
- Мелкие курсы за единицу передаются в экспоненциальной записи (IRR: `<VunitRate>4,8189E-05</VunitRate>`, номинал 1 000 000) → `NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent`.
- `VunitRate` есть не для всех исторических дат → fallback `Value / Nominal`.
- Отсутствие `ValCurs/@Date`, невалидный XML, нечисловые значения → `CbrUnavailableException` → 502 (AC-8.2).
- `ValCurs` без `Valute` при валидной дате → пустой список (A-8).

## 5. Поток обработки запроса

1. `CurrencyRatesController.Get(string? date, string? code, CancellationToken ct)`.
2. `RatesQueryValidator.Validate(date, code, today)` → либо ошибки (→ `ValidationProblem()` 400), либо `(DateOnly requestedDate, string? code)`. `today` = `MoscowClock.Today`.
3. `CurrencyRatesService.GetRatesAsync(requestedDate, code, ct)`:
   1. `IMemoryCache.GetOrCreateAsync(key, …)` → при промахе `ICbrRatesClient.GetDailyRatesAsync(requestedDate, ct)`; TTL по правилу п. 4.2.
   2. Если `code == null` → вернуть все курсы.
   3. Иначе найти `CharCode` с `StringComparison.OrdinalIgnoreCase`; не найдено → `null`.
4. Контроллер: `null` → `NoContent()` (204); иначе `Ok(CurrencyRatesResponse)` (200).
5. Исключения `CbrUnavailableException` / `CbrTimeoutException` перехватывает `CbrExceptionHandler` → 502 / 504.

## 6. Архитектурные решения (ADR)

### ADR-1. Источник данных — `XML_daily.asp`
- **Контекст.** ЦБ предоставляет несколько способов получения курсов; нужно выбрать оптимальный (Task.md). Требуются все валюты на дату и фактическая дата курса.
- **Решение.** `https://www.cbr.ru/scripts/XML_daily.asp?date_req=dd/MM/yyyy`.
- **Альтернативы.**
  - SOAP `DailyInfoWebServ.asmx` (`GetCursOnDateXML`) — тот же набор данных, но тяжелее протокол (SOAP-конверт, WSDL/генерация прокси) без выигрыша для задачи.
  - `XML_dynamic.asp` — динамика по одной валюте за период; не возвращает все валюты, требует внутренний код `R01235`.
  - Неофициальные JSON-зеркала (cbr-xml-daily.ru) — не официальный источник, дополнительная внешняя зависимость.
- **Последствия.** Один HTTP-запрос на дату покрывает и «все валюты», и «одну валюту» → эффективное кэширование по дате. Нужно обрабатывать `windows-1251` и запятую в числах. Фактическая дата в ответе даёт FR-5 «бесплатно».
- **Закрывает:** FR-1, FR-3, FR-5, FR-6.

### ADR-2. Один проект с разделением на папки
- **Контекст.** Один эндпоинт, одна интеграция, нет БД.
- **Решение.** Один веб-проект; слои — папками `Api / Application / Domain / Infrastructure`; зависимости через интерфейсы (`ICurrencyRatesService`, `ICbrRatesClient`).
- **Альтернативы.** Clean Architecture с отдельными проектами на слой; CQRS/MediatR.
- **Последствия.** Минимум церемоний при сохранении тестируемости. Отказ от многопроектной структуры и MediatR: для одного сценария чтения они добавляют код, но не ценность. При росте сервиса папки легко вынести в проекты.
- **Закрывает:** NFR-7 (через интерфейсы и DI).

### ADR-3. Контроллеры, а не Minimal API
- **Контекст.** Шаблон проекта уже на контроллерах и Swashbuckle.
- **Решение.** `[ApiController]`-контроллер с атрибутами `[ProducesResponseType]`.
- **Альтернативы.** Minimal API + `TypedResults` — компактнее, но для одного эндпоинта выигрыш незначителен.
- **Последствия.** Привычная структура, явное описание ответов для Swagger/OpenAPI (D-1), встроенный `ValidationProblem()`.
- **Закрывает:** FR-1…FR-8 (HTTP-контракт).

### ADR-4. Кэш в памяти процесса (`IMemoryCache`), ключ — запрошенная дата
- **Контекст.** NFR-1; один экземпляр (A-4).
- **Решение.** `IMemoryCache`, кэшируются все курсы на запрошенную дату; TTL 24 ч для прошедших дат, 15 мин для текущей и будущей (настраиваемо). Время берётся из `TimeProvider`.
- **Альтернативы.**
  - Redis / `IDistributedCache` — избыточен для одного экземпляра (out of scope).
  - Ключ по фактической дате — экономит память для выходных, но требует знать фактическую дату до запроса к ЦБ; выигрыш не стоит усложнения.
  - `HybridCache` — появился в .NET 9.
- **Последствия.** Кэш теряется при перезапуске (A-4). Одновременные промахи по одной дате могут дать несколько параллельных запросов к ЦБ — допустимо при текущей нагрузке (см. предложения, п. 10).
- **Закрывает:** NFR-1, NFR-2 (кэшированные данные доступны при сбое ЦБ).

### ADR-5. Устойчивость — `Microsoft.Extensions.Http.Resilience`
- **Контекст.** NFR-2, FR-8.
- **Решение.** Typed `HttpClient` + `AddStandardResilienceHandler()` с настройками из конфигурации: таймаут попытки 5 с, общий таймаут 15 с, 2 повтора с экспоненциальной задержкой и jitter, circuit breaker по умолчанию. `HttpClient.Timeout = Infinite` — таймаутами управляет pipeline.
- **Альтернативы.** Ручная настройка Polly — больше кода для того же результата; без повторов — нарушает NFR-2.
- **Последствия.** Повторы по 5xx/408/429/сетевым ошибкам из коробки. Маппинг в `CbrRatesClient`:
  - `TimeoutRejectedException` → `CbrTimeoutException` (504);
  - `HttpRequestException`, `BrokenCircuitException`, неуспешный статус после повторов, ошибка разбора → `CbrUnavailableException` (502);
  - `OperationCanceledException` при отменённом `RequestAborted` клиента пробрасывается как есть (не 504).
- **Закрывает:** NFR-2, FR-8.

### ADR-6. Московское время — фиксированное смещение UTC+3 поверх `TimeProvider`
- **Контекст.** FR-2, FR-7 (граница «завтра»).
- **Решение.** `MoscowClock.Today = DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(+03:00).DateTime)`. `TimeProvider` регистрируется в DI (`TimeProvider.System`), в тестах — `FakeTimeProvider`.
- **Альтернативы.** `TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow")` — зависит от наличия tzdata/ICU в образе и различий Windows/Linux.
- **Последствия.** С 2014 г. в Москве нет перехода на летнее время; используется только для вычисления «сегодня», исторические смещения не нужны. При изменении законодательства потребуется правка константы.
- **Закрывает:** FR-2, FR-7, AC-2.1, AC-7.3.

### ADR-7. Валидация — собственный валидатор без библиотек
- **Контекст.** FR-7: две простые проверки, нужно вернуть все ошибки сразу (AC-7.6).
- **Решение.** Статический `RatesQueryValidator`, возвращающий `Dictionary<string, string[]>`; контроллер отдаёт `ValidationProblem(new ValidationProblemDetails(errors))`.
- **Альтернативы.** FluentValidation — лишняя зависимость для двух правил; DataAnnotations — не выражают правило «не позже завтра по МСК».
- **Последствия.** Правила тестируются unit-тестами без хоста.
- **Закрывает:** FR-7.

### ADR-8. Ошибки источника — типизированные исключения + `IExceptionHandler`
- **Контекст.** FR-8, NFR-3.
- **Решение.** Клиент ЦБ бросает `CbrUnavailableException` / `CbrTimeoutException`; `CbrExceptionHandler : IExceptionHandler` (.NET 8) превращает их в ProblemDetails 502/504 через `IProblemDetailsService`. Прочие исключения — 500 стандартным обработчиком `UseExceptionHandler()`; детали исключения в ответ не попадают.
- **Альтернативы.** Result-тип (`OneOf`/свой) по всей цепочке — больше кода для двух ошибочных веток.
- **Последствия.** Контроллер и сервис содержат только «счастливый путь»; сбои источника логируются в одном месте.
- **Закрывает:** FR-8, NFR-3, NFR-4.

### ADR-9. Тестирование — `WebApplicationFactory` с заглушкой ЦБ
- **Контекст.** NFR-7: тесты не ходят на реальный cbr.ru.
- **Решение.** xUnit; интеграционные тесты через `WebApplicationFactory<Program>`; подмена primary `HttpMessageHandler` типизированного клиента на `StubCbrHandler` (настраиваемые ответы, счётчик вызовов, задержка); `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) для «текущей даты» и истечения кэша. Resilience-pipeline остаётся реальным (с уменьшенными таймаутами в тестовой конфигурации) — проверяются AC-8.3 и AC-N2.1.
- **Альтернативы.** WireMock.Net — реальный HTTP-сервер, но тяжелее; моки интерфейса клиента — не покрывают HTTP-уровень и resilience.
- **Последствия.** Для истечения кэша (AC-N1.2) `MemoryCacheOptions.Clock` подключается к `FakeTimeProvider` через адаптер.
- **Закрывает:** NFR-7, все AC.

### ADR-10. OpenAPI — генерация из кода со snapshot-проверкой
- **Контекст.** D-1, A-11. Ручной YAML при росте числа эндпоинтов расходится с кодом.
- **Решение.** Описание строит Swashbuckle из контроллеров, `[ProducesResponseType]` и XML-комментариев; форматы параметров (`date` — `format: date`, `code` — `pattern`) задаёт operation filter по константам `RatesQueryValidator`, чтобы описание не расходилось с валидацией. Файл `specs/openapi.yaml` хранится в репозитории. Тест генерирует документ тем же `ISwaggerProvider` и сравнивает с файлом; при расхождении падает. Обновление файла — тот же тест с переменной окружения `UPDATE_OPENAPI=1`.
- **Альтернативы.**
  - Ручной YAML (design-first) — точный контракт, но ручная поддержка и риск расхождения.
  - Swashbuckle CLI (`dotnet swagger tofile`) + `git diff --exit-code` в CI — отдельный инструмент, проверка только в CI и только при наличии git.
- **Последствия.** Проверка выполняется в обычном `dotnet test` — локально и в Jenkins без дополнительных шагов. Изменения контракта видны в diff `specs/openapi.yaml` при ревью.
- **Закрывает:** D-1.

### ADR-11. Контейнеризация и CI
- **Контекст.** D-2, NFR-6, A-10.
- **Решение.** Multi-stage `Dockerfile` (`sdk:8.0` → `aspnet:8.0`, непривилегированный пользователь, порт 8080). `Jenkinsfile` (declarative): restore → build → test (отчёт JUnit/TRX) → `docker build` → необязательный `docker push` (параметр `PUSH_IMAGE`, по умолчанию `false`; адрес registry и ID credentials — параметры с условными значениями).
- **Альтернативы.** Сборка внутри контейнера-агента `dotnet/sdk` — требует Docker-in-Docker для шага `docker build`; Kaniko/Buildah — избыточно для задачи.
- **Последствия.** Пайплайн не привязан к конкретной инфраструктуре; для реального использования достаточно задать параметры registry и создать credentials в Jenkins.
- **Закрывает:** D-2, NFR-6.

## 7. Сквозная функциональность

### 7.1. Валидация
`RatesQueryValidator` (ADR-7). Сообщения на русском, ключи ошибок — имена query-параметров (`date`, `code`).

| Правило | Сообщение | AC |
|---|---|---|
| `date` не `yyyy-MM-dd` / несуществующая дата | Дата должна быть в формате yyyy-MM-dd. | AC-7.1, AC-7.2 |
| `date > today + 1` | Дата не может быть позже {завтра}. | AC-7.3 |
| `date < 1992-07-01` | Дата не может быть раньше 1992-07-01. | AC-7.4 |
| `code` не `^[A-Za-z]{3}$` | Код валюты должен состоять из трёх латинских букв (ISO 4217). | AC-7.5 |

### 7.2. Обработка ошибок
`AddProblemDetails()` + `AddExceptionHandler<CbrExceptionHandler>()` + `UseExceptionHandler()` + `UseStatusCodePages()` (ADR-8).

### 7.3. Логирование
- Встроенный `ILogger`; в Production — `AddJsonConsole()` (структурированные логи в stdout для сбора в контейнерной среде).
- Входящие запросы и их длительность — встроенные логи хостинга (`Microsoft.AspNetCore.Hosting.Diagnostics`).
- Исходящие запросы к ЦБ — встроенные логи `System.Net.Http.HttpClient.CbrRatesClient.*` (URL, статус, длительность) + события resilience-pipeline (повторы).
- Собственные события: промах/попадание кэша (Debug), фактическая дата и число валют (Information), ошибки ЦБ (Warning/Error, в `CbrExceptionHandler`).

### 7.4. Health checks
`AddHealthChecks()` + `MapHealthChecks("/health")`; только liveness (п. 3.5).

### 7.5. Конфигурация
Options pattern с `ValidateDataAnnotations().ValidateOnStart()`; переопределение через переменные окружения (`Cbr__BaseUrl`, `RatesCache__CurrentDatesTtl` и т.д.).

```json
{
  "Cbr": {
    "BaseUrl": "https://www.cbr.ru/",
    "DailyRatesPath": "scripts/XML_daily.asp",
    "AttemptTimeout": "00:00:05",
    "TotalTimeout": "00:00:15",
    "MaxRetryAttempts": 2,
    "RetryBaseDelay": "00:00:00.500"
  },
  "RatesCache": {
    "CurrentDatesTtl": "00:15:00",
    "PastDatesTtl": "1.00:00:00"
  }
}
```

`CbrRatesClient` отправляет заголовок `User-Agent` (сайт ЦБ может отклонять запросы без него).

### 7.6. Прочее
- `UseHttpsRedirection()` убирается: сервис внутренний, в контейнере слушает HTTP, TLS — на уровне инфраструктуры. HTTPS используется для исходящих запросов к ЦБ (NFR-3).
- Swagger UI — только в Development.

## 8. Пакеты

| Пакет | Проект | Зачем |
|---|---|---|
| `Swashbuckle.AspNetCore` 6.9.0 (обновлён с 6.6.2 шаблона) | веб | Swagger UI в Development, генерация OpenAPI (ADR-10); 6.7+ нужен для `NonNullableReferenceTypesAsRequired` |
| `Microsoft.Extensions.Http.Resilience` 8.x | веб | ADR-5 |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | тесты | тест-раннер |
| `Microsoft.AspNetCore.Mvc.Testing` 8.x | тесты | `WebApplicationFactory` |
| `Microsoft.Extensions.TimeProvider.Testing` 8.x | тесты | `FakeTimeProvider` |

`System.Text.Encoding.CodePages` входит в shared framework .NET 8 — отдельный пакет не нужен.

## 9. Трассируемость: требования → решения

| Требование | Компоненты / решения |
|---|---|
| FR-1 | `CurrencyRatesController`, `CurrencyRatesService`, `CbrRatesClient`; ADR-1, ADR-3 |
| FR-2 | `MoscowClock`, `TimeProvider`; ADR-6 |
| FR-3 | `CurrencyRatesService` (фильтр `OrdinalIgnoreCase`); ADR-1 |
| FR-4 | `CurrencyRatesService` → `null` → `NoContent()` |
| FR-5 | `CbrXmlParser` (`ValCurs/@Date`), `CurrencyRatesResponse.RateDate`; ADR-1 |
| FR-6 | `CbrXmlParser`, `CurrencyRateDto`; п. 3.3, 4.3 |
| FR-7 | `RatesQueryValidator`; ADR-6, ADR-7 |
| FR-8 | `CbrRatesClient`, `CbrExceptionHandler`; ADR-5, ADR-8 |
| NFR-1 | `IMemoryCache` в `CurrencyRatesService`; ADR-4 |
| NFR-2 | Standard resilience handler; ADR-4, ADR-5 |
| NFR-3 | Валидация, ProblemDetails без деталей, HTTPS к ЦБ; ADR-8 |
| NFR-4 | Логирование (п. 7.3), `/health` (п. 7.4) |
| NFR-5 | Options pattern (п. 7.5) |
| NFR-6 | .NET 8, HTTP в контейнере (п. 7.6); Dockerfile — D-2 |
| NFR-7 | Интерфейсы + DI, `TimeProvider`; ADR-9 |

Эндпоинт → требования: `GET /api/v1/currency-rates` — FR-1…FR-8; `GET /health` — NFR-4.

## 10. Предложения вне спецификации (не реализуются)

1. Защита от «stampede»: при одновременных промахах кэша по одной дате выполнять один запрос к ЦБ (single-flight через `Lazy<Task>` / `HybridCache` после перехода на .NET 9).
2. Отдавать устаревшие данные из кэша при недоступности ЦБ (stale-if-error) для текущей даты.
3. HTTP-кэширование для клиентов (`Cache-Control`, `ETag`) — прошедшие даты неизменны.
4. Метрики OpenTelemetry (попадания в кэш, латентность ЦБ) и трассировка.
5. Readiness-проба с проверкой доступности ЦБ (отдельно от liveness).
