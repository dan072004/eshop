using System.Net;
using System.Text;
using System.Text.Json;

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

    /// <summary>Serialises <c>body</c> the way ASP.NET Core APIs do (web defaults, so camelCase names).</summary>
    public static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        Json(status, WireJson(body).GetRawText());

    /// <summary>The JSON <see cref="Json(HttpStatusCode, object)"/> sends for <c>body</c>.</summary>
    public static JsonElement WireJson(object body) => JsonSerializer.SerializeToElement(body, JsonSerializerOptions.Web);

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
