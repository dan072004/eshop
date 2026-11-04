using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace eShop.Mcp;

/// <summary>
/// Sends Ordering.API a token for the caller, never the caller's own token (no passthrough, ADR 0005). On every request
/// it exchanges the caller's validated eShop.Mcp token for an Ordering token and sends that as Bearer. If the exchange is
/// refused, or there is no caller token, it answers 401 without sending, which the order tools return as
/// <c>sign_in_required</c>. If Identity.API fails, the <see cref="TokenExchangeUnavailableException"/> propagates and the
/// order tools return <c>unavailable</c>. Either way Ordering.API receives no request.
/// </summary>
internal sealed class OrdersTokenExchangeHandler(IHttpContextAccessor httpContextAccessor, TokenExchangeClient tokenExchange)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The token JwtBearer validated on this request (SaveToken).
        var callerToken = httpContextAccessor.HttpContext is { } context
            ? await context.GetTokenAsync("access_token")
            : null;

        var ordersToken = callerToken is null
            ? null
            : await tokenExchange.ExchangeForOrdersTokenAsync(callerToken, cancellationToken);

        if (ordersToken is null)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ordersToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
