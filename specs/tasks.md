# План задач: шлюз курсов валют Банка России

| Поле | Значение |
|---|---|
| Версия | 1.0 |
| Дата | 2026-10-03 |
| Статус | Согласовано (Этап 4) |
| Основание | `specs/spec.md` v1.0, `specs/design.md` v1.0 |

Статусы: `[ ]` — не начата, `[~]` — в работе, `[x]` — выполнена.

Общий критерий готовности любой задачи: `dotnet build` без ошибок и предупреждений nullable, `dotnet test` зелёный.

## Очередь 1 — основная задача

### T-01. Подготовка решения `[x]`
- **Описание.** Удалить шаблонные `WeatherForecast*`. Создать проект `DigitalLeagueTask.Tests` (xUnit, net8.0) и добавить его в `DigitalLeagueTask.slnx`. Подключить пакеты по design.md п. 8. Добавить `public partial class Program;` для `WebApplicationFactory`. Убрать `UseHttpsRedirection()`.
- **Требования.** NFR-6, NFR-7.
- **Готово, когда.** Решение собирается, тестовый проект запускается (0 тестов), шаблонного кода не осталось.

### T-02. Доменная модель и контракты ответа `[x]`
- **Описание.** `Domain/CurrencyRate`, `Domain/DailyRates`; `Api/Contracts/CurrencyRatesResponse`, `CurrencyRateDto` с фабрикой из доменной модели.
- **Требования.** FR-5, FR-6.
- **Готово, когда.** Типы соответствуют design.md п. 3.3 и 4.1; unit-тест сериализации: `DateOnly` → `yyyy-MM-dd`, `decimal` → число, имена camelCase.

### T-03. Парсер XML ЦБ `[x]`
- **Описание.** `Infrastructure/Cbr/CbrXmlParser`: поток → `DailyRates`; кодировка `windows-1251`, десятичная запятая, fallback `UnitRate = Value / Nominal`, пустой `ValCurs` → пустой список; невалидные данные → `CbrUnavailableException`. Образцы XML в `Tests/TestData`.
- **Требования.** FR-5, FR-6, FR-8, A-8.
- **Готово, когда.** Unit-тесты: корректный разбор (включая JPY с номиналом 100 и кириллицу), фактическая дата из `ValCurs/@Date`, отсутствие `VunitRate`, пустой `ValCurs`, невалидный XML / нет `Date` / нечисловое значение → исключение.

### T-04. Московские часы `[x]`
- **Описание.** `Application/MoscowClock` поверх `TimeProvider` (UTC+3).
- **Требования.** FR-2.
- **Готово, когда.** Unit-тест: 2026-09-30 23:30 UTC → `Today = 2026-10-01`; 2026-09-30 20:59 UTC → `2026-09-30`.

### T-05. Валидатор параметров `[x]`
- **Описание.** `Api/RatesQueryValidator`: строгий формат даты, диапазон `1992-07-01 … завтра МСК`, код `^[A-Za-z]{3}$`, сбор всех ошибок; дата по умолчанию = сегодня МСК.
- **Требования.** FR-2, FR-7.
- **Готово, когда.** Unit-тесты на все кейсы AC-7.1…AC-7.6 и на подстановку даты по умолчанию.

### T-06. HTTP-клиент ЦБ с устойчивостью `[x]`
- **Описание.** `CbrOptions` (валидация, `ValidateOnStart`), `ICbrRatesClient`/`CbrRatesClient` (typed `HttpClient`, `User-Agent`, формат `date_req=dd/MM/yyyy`), `AddStandardResilienceHandler` с параметрами из конфигурации, маппинг ошибок в `CbrUnavailableException` / `CbrTimeoutException`, проброс отмены клиентом. Регистрация `CodePagesEncodingProvider`.
- **Требования.** FR-1, FR-8, NFR-2, NFR-3, NFR-5.
- **Готово, когда.** Код компилируется, регистрация в DI вынесена в extension-метод; поведение проверяется интеграционными тестами T-11.

### T-07. Сервис курсов с кэшем `[x]`
- **Описание.** `RatesCacheOptions`, `ICurrencyRatesService`/`CurrencyRatesService`: `IMemoryCache` по ключу запрошенной даты, TTL по правилу design.md п. 4.2, фильтр по коду без учёта регистра, `null` при отсутствии валюты. Логи попаданий/промахов.
- **Требования.** FR-1, FR-3, FR-4, NFR-1, NFR-4.
- **Готово, когда.** Unit-тесты с фейковым `ICbrRatesClient`: фильтр `usd`/`USD`, отсутствующая валюта → `null`, повторный запрос → клиент вызван 1 раз, ошибка клиента не кэшируется.

### T-08. Обработка ошибок `[x]`
- **Описание.** `ErrorHandling/CbrExceptionHandler : IExceptionHandler` → ProblemDetails 502/504 с логированием; `AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`.
- **Требования.** FR-8, NFR-3, NFR-4.
- **Готово, когда.** Код компилируется; поведение проверяется интеграционными тестами T-11.

### T-09. Контроллер и композиция приложения `[x]`
- **Описание.** `Api/CurrencyRatesController` (`GET /api/v1/currency-rates`, `[ProducesResponseType]` для 200/204/400/502/504). `Program.cs`: DI, options, `TimeProvider.System`, `AddMemoryCache`, health checks `/health`, JSON-логи в Production, Swagger в Development. `appsettings.json` с секциями `Cbr`, `RatesCache`. Обновить `DigitalLeagueTask.http`.
- **Требования.** FR-1…FR-8, NFR-4, NFR-5.
- **Готово, когда.** Приложение запускается, `GET /health` → 200, Swagger показывает эндпоинт со всеми кодами ответов, ручной запрос к реальному ЦБ возвращает курсы.

### T-10. Инфраструктура интеграционных тестов `[x]`
- **Описание.** `CbrApiFactory : WebApplicationFactory<Program>`: подмена primary handler клиента ЦБ на `StubCbrHandler` (очередь ответов / исключений / задержка, счётчик и журнал запросов), `FakeTimeProvider`, адаптер часов для `MemoryCacheOptions.Clock`, уменьшенные таймауты и задержки повторов.
- **Требования.** NFR-7.
- **Готово, когда.** Smoke-тест через фабрику проходит без обращения к сети.

### T-11. Интеграционные тесты критериев приёмки `[x]`
- **Описание.** Тесты через HTTP на все AC (матрица ниже).
- **Требования.** FR-1…FR-8, NFR-1, NFR-2, NFR-4, NFR-7.
- **Готово, когда.** Каждый AC из spec.md покрыт хотя бы одним тестом, все тесты зелёные.

### T-12. Закрытие очереди 1 `[x]`
- **Описание.** Сверка трассируемости (FR/NFR → задача → тест), актуализация статусов в tasks.md и спецификации, фиксация отклонений (если были — через правку спецификации).
- **Требования.** все.
- **Готово, когда.** Матрица трассируемости заполнена без пропусков.

## Очередь 2 — дополнительные задачи

### T-13. Асинхронная переработка — описание `[x]`
- **Описание.** Несколько предложений о переработке сервиса для асинхронного получения данных ЦБ — раздел в `README.md` в корне репозитория (отображается на GitHub).
- **Требования.** D-3.
- **Готово, когда.** Текст согласован.

### T-14. OpenAPI 3.0 (YAML) `[x]`
- **Описание.** Генерация `specs/openapi.yaml` из кода (design.md, ADR-10): XML-документация, описание сервиса, форматы параметров по константам валидатора; snapshot-тест актуальности файла с режимом обновления `UPDATE_OPENAPI=1`.
- **Требования.** D-1.
- **Готово, когда.** Файл в формате OpenAPI 3.0 описывает все коды ответов, схемы и форматы параметров; тест падает при расхождении кода и файла.

### T-15. Dockerfile и Jenkins-пайплайн `[x]`
- **Описание.** Multi-stage `Dockerfile` (sdk → aspnet, non-root, `ASPNETCORE_HTTP_PORTS=8080`), `.dockerignore`, `Jenkinsfile` (checkout → restore → build → test с отчётом → docker build → опциональный push по параметру). Registry и credentials — параметры с условными значениями (A-10; design.md, ADR-11).
- **Требования.** D-2, NFR-6.
- **Готово, когда.** `docker build` проходит, контейнер отвечает на `/health`; Jenkinsfile синтаксически корректен.

## Матрица трассируемости: критерии приёмки → тесты

Unit-тесты — `DigitalLeagueTask.Tests/Unit`, интеграционные — `DigitalLeagueTask.Tests/Integration/CurrencyRatesApiTests` (имя теста начинается с номера AC).

| AC | Unit | Интеграционный |
|---|---|---|
| AC-1.1 | `CbrXmlParserTests.Parses_real_cbr_response_for_working_day` | `AC_1_1_returns_all_currencies_for_date` |
| AC-2.1 | `MoscowClockTests.Today_is_calculated_in_moscow_time` | `AC_2_1_uses_today_in_moscow_time_when_date_is_not_passed` |
| AC-3.1, AC-3.2 | `CurrencyRatesServiceTests.Returns_single_currency_by_code_case_insensitive`, `RatesQueryValidatorTests.Accepts_date_and_normalizes_code_to_upper_case` | `AC_3_1_and_3_2_returns_single_currency_by_code_case_insensitive` |
| AC-4.1 | `CurrencyRatesServiceTests.Returns_null_when_currency_is_absent` | `AC_4_1_returns_204_without_body_when_currency_is_absent` |
| AC-5.1 | `CbrXmlParserTests.Parses_real_cbr_response_with_actual_rate_date` | `AC_5_1_returns_last_effective_rate_with_actual_date_for_weekend` |
| AC-6.1 | `CbrXmlParserTests.Parses_all_currency_fields_including_nominal_and_cyrillic_name`, `CurrencyRatesResponseSerializationTests` | `AC_6_1_returns_all_currency_fields_as_json_numbers` |
| AC-7.1, AC-7.2 | `RatesQueryValidatorTests.Rejects_date_in_wrong_format` | `AC_7_returns_400_problem_details_for_invalid_parameter` |
| AC-7.3 | `RatesQueryValidatorTests.Rejects_date_after_tomorrow_and_accepts_tomorrow` | `AC_7_returns_400_…`, `AC_7_3_accepts_tomorrow` |
| AC-7.4 | `RatesQueryValidatorTests.Rejects_date_before_first_cbr_publication_and_accepts_boundary` | `AC_7_returns_400_…`, `AC_7_4_accepts_first_publication_date` |
| AC-7.5 | `RatesQueryValidatorTests.Rejects_code_that_is_not_three_latin_letters` | `AC_7_returns_400_problem_details_for_invalid_parameter` |
| AC-7.6 | `RatesQueryValidatorTests.Reports_errors_for_both_parameters_at_once` | `AC_7_6_reports_errors_for_both_parameters` |
| AC-8.1 | `CbrRatesClientTests.Throws_CbrUnavailableException_for_unsuccessful_status` | `AC_8_1_returns_502_when_cbr_fails_on_all_attempts` |
| AC-8.2 | `CbrXmlParserTests.Throws_CbrUnavailableException_for_invalid_response` | `AC_8_2_returns_502_when_cbr_returns_invalid_xml` |
| AC-8.3 | — | `AC_8_3_returns_504_when_cbr_does_not_respond_in_time` |
| AC-N1.1 | `CurrencyRatesServiceTests.Does_not_call_cbr_again_for_cached_past_date_with_any_code` | `AC_N1_1_serves_repeated_past_date_requests_from_cache` |
| AC-N1.2 | `CurrencyRatesServiceTests.Calls_cbr_again_when_current_date_ttl_expires` | `AC_N1_2_requests_cbr_again_after_current_date_ttl_expires` |
| AC-N2.1 | — | `AC_N2_1_retries_transient_cbr_failure` |
| AC-N4.1 | — | `AC_N4_1_health_check_does_not_depend_on_cbr` |

Дополнительно (без отдельного AC): экспоненциальная запись курса (`Parses_unit_rate_in_exponential_notation`), ведущие нули цифрового кода, отсутствие `VunitRate`, пустой ответ (A-8), отмена запроса клиентом, валидация настроек (NFR-5, `CbrServiceCollectionExtensionsTests`), некэширование ошибок ЦБ.

## Покрытие требований задачами

| Требование | Задачи |
|---|---|
| FR-1 | T-06, T-07, T-09, T-11 |
| FR-2 | T-04, T-05, T-11 |
| FR-3 | T-07, T-09, T-11 |
| FR-4 | T-07, T-09, T-11 |
| FR-5 | T-02, T-03, T-11 |
| FR-6 | T-02, T-03, T-11 |
| FR-7 | T-05, T-09, T-11 |
| FR-8 | T-03, T-06, T-08, T-11 |
| NFR-1 | T-07, T-11 |
| NFR-2 | T-06, T-11 |
| NFR-3 | T-06, T-08 (косвенно — тесты AC-8.1, AC-8.2, AC-8.3) |
| NFR-4 | T-07, T-08, T-09, T-11 |
| NFR-5 | T-06, T-09 (`CbrServiceCollectionExtensionsTests`) |
| NFR-6 | T-01, T-15 |
| NFR-7 | T-01, T-10, T-11 |
| D-1 | T-14 |
| D-2 | T-15 |
| D-3 | T-13 |

## Требования без автоматической проверки

| Требование | Что не покрыто | Решение |
|---|---|---|
| NFR-1 | p95 ≤ 50 мс при наличии данных в кэше | Нагрузочное тестирование избыточно для масштаба задачи; автотестами не проверяется. Отсутствие обращений к ЦБ при попадании в кэш проверяется (AC-N1.1). |
| NFR-3 | Отсутствие внутренних деталей в ответах об ошибках | Отдельного теста нет; проверяется косвенно в тестах AC-8.1, AC-8.2, AC-8.3 (в теле нет имён исключений, стека вызовов и адреса источника). |
| NFR-6 | Работа в Linux-контейнере | На машине разработки Docker нет: `docker build` и запуск контейнера локально не проверялись. Проверено: `dotnet publish` той же командой, что в Dockerfile, и запуск публикации в режиме контейнера (Production, порт 8080, JSON-логи, `/health`, реальный запрос к ЦБ). Сборку образа и `/health` в контейнере автоматически проверяет этап `Smoke Test` в Jenkinsfile. |

## Итог очереди 1

- Задачи T-01 … T-12 выполнены; 75 автотестов, все зелёные.
- Все FR-1 … FR-8 и все критерии приёмки покрыты тестами; без автоматической проверки — только пункты из таблицы выше.
- Изменения спецификации в ходе реализации: v1.1 и v1.2 — только примеры в AC (реальные данные ЦБ, опорная дата 2026-10-01); поведение не менялось.
- Уточнения дизайна в ходе реализации: экспоненциальная запись чисел у ЦБ (п. 4.3), настройка `RetryBaseDelay` (п. 7.5), `traceId` во всех ProblemDetails (п. 3.4).

## Итог очереди 2

- T-13: раздел «Асинхронное получение данных ЦБ» в `README.md` (D-3).
- T-14: `specs/openapi.yaml` генерируется из кода; актуальность проверяет `OpenApiDocumentTests` (D-1). Swashbuckle обновлён до 6.9.0.
- T-15: `Dockerfile`, `.dockerignore`, `Jenkinsfile` (D-2). Тестовый проект дополнен `JunitXml.TestLogger` для отчёта в Jenkins. Сборка образа локально не проверена (нет Docker) — см. NFR-6 выше.
