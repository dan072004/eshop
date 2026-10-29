using System.Net;

namespace eShop.Mcp;

/// <summary>
/// Forwards the caller's token to Ordering.API only when it names the <c>orders</c> audience (ADR 0005). Ordering.API
/// doesn't validate the audience itself, so without this check any token issued for eShop.Mcp alone would pass
/// through. Otherwise it answers 401 without sending, which the order tools return as <c>sign_in_required</c>.
/// </summary>
internal sealed class OrdersAudienceHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string OrdersAudience = "orders";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.HasClaim("aud", OrdersAudience) != true)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request });
        }

        return base.SendAsync(request, cancellationToken);
    }
}
