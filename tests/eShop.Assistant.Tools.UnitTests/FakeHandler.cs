using System.Net;
using System.Text;

namespace eShop.Assistant.Tools.UnitTests;

/// <summary>
/// Fakes outbound HTTP, a boundary we don't own: answers each request with <c>respond</c> (which may also throw, to
/// stand in for a transport failure) and records the requests.
/// </summary>
internal sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }
        return Task.FromResult(respond(request));
    }
}

/// <summary>The clock is a boundary we don't own.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
