using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace DigitalLeagueTask.Tests.Support;

/// <summary>
/// Заглушка сайта ЦБ на уровне HttpMessageHandler: ответы выдаются по очереди,
/// после исчерпания очереди повторяется последний. Запросы журналируются.
/// </summary>
internal sealed class StubCbrHandler : HttpMessageHandler
{
    private readonly object _sync = new();
    private readonly Queue<Responder> _responders = new();
    private Responder? _last;

    public delegate Task<HttpResponseMessage> Responder(HttpRequestMessage request, CancellationToken cancellationToken);

    public ConcurrentQueue<Uri> Requests { get; } = new();

    public int CallCount => Requests.Count;

    public StubCbrHandler Then(Responder responder)
    {
        lock (_sync)
        {
            _responders.Enqueue(responder);
        }

        return this;
    }

    public StubCbrHandler ThenXml(byte[] windows1251Xml) => Then((_, _) => Task.FromResult(Xml(windows1251Xml)));

    public StubCbrHandler ThenXml(string xml) => ThenXml(CbrXml.ToBytes(xml));

    public StubCbrHandler ThenFile(string fileName) => ThenXml(CbrXml.ReadFile(fileName));

    public StubCbrHandler ThenStatus(HttpStatusCode statusCode) =>
        Then((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    public StubCbrHandler ThenThrow(Exception exception) => Then((_, _) => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Не отвечает, пока запрос не будет отменён (таймаутом или клиентом).</summary>
    public StubCbrHandler ThenHang() => Then(async (_, cancellationToken) =>
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Недостижимо.");
    });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request.RequestUri!);

        Responder? responder;
        lock (_sync)
        {
            if (_responders.TryDequeue(out var next))
            {
                _last = next;
            }

            responder = _last;
        }

        return responder is null
            ? throw new InvalidOperationException("Для заглушки ЦБ не задан ответ.")
            : responder(request, cancellationToken);
    }

    private static HttpResponseMessage Xml(byte[] content)
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "windows-1251" };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = body };
    }
}
