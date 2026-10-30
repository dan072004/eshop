using System.Net.Http.Headers;

namespace eShop.WebApp.Services.AccessTokens;

/// <summary>
/// Adds the current circuit's <see cref="AccessTokenHolder"/> token as a Bearer token. With no token (signed out, or
/// called outside a circuit) it sends no Authorization header, and the backend answers 401.
/// </summary>
public sealed class AccessTokenHandler(CircuitServicesAccessor circuitServices) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = circuitServices.Services?.GetService<AccessTokenHolder>()?.AccessToken;
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
