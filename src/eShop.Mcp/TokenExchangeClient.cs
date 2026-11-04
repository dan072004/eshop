using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace eShop.Mcp;

/// <summary>
/// Exchanges a Buyer's eShop.Mcp token for an Ordering token at Identity.API's token endpoint (RFC 8693, ADR 0005),
/// authenticating as the confidential client <c>eshop-mcp-server</c>. Nothing is cached: every call exchanges again.
/// Public only so the functional tests can fake its outbound handler.
/// </summary>
public sealed class TokenExchangeClient(HttpClient httpClient, IConfiguration configuration)
{
    public const string ClientId = "eshop-mcp-server";
    public const string ClientSecretSetting = "EshopMcpServerSecret";

    private const string TokenExchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";
    private const string OrdersScope = "orders";

    private readonly string _clientSecret = configuration.GetRequiredValue(ClientSecretSetting);

    /// <summary>The Ordering token for the Buyer, or <see langword="null"/> when Identity.API refuses the exchange.</summary>
    /// <exception cref="TokenExchangeUnavailableException">Identity.API could not be reached or failed.</exception>
    public async Task<string?> ExchangeForOrdersTokenAsync(string subjectToken, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = TokenExchangeGrant,
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = AccessTokenType,
            ["scope"] = OrdersScope,
            ["client_id"] = ClientId,
            ["client_secret"] = _clientSecret,
        });

        try
        {
            using var response = await httpClient.PostAsync("connect/token", content, cancellationToken);

            // RFC 6749 §5.2: a refused grant, scope or client is 400 (invalid_grant, invalid_scope, ...), or 401 for
            // invalid_client. The Buyer has to sign in again.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            return string.IsNullOrEmpty(token?.AccessToken)
                ? throw new TokenExchangeUnavailableException("Identity.API's token response has no access token.", null)
                : token.AccessToken;
        }
        catch (Exception e) when (e is not TokenExchangeUnavailableException
            && (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            throw new TokenExchangeUnavailableException("The token exchange with Identity.API failed.", e);
        }
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);
}

/// <summary>
/// Identity.API could not exchange the token (unreachable, 5xx, timed out, or an unreadable answer). Deliberately not an
/// <see cref="HttpRequestException"/>, so the Ordering client's resilience pipeline, which the exchange runs inside,
/// does not retry it: the order tools return <c>unavailable</c> after one exchange.
/// </summary>
public sealed class TokenExchangeUnavailableException(string message, Exception? innerException)
    : Exception(message, innerException);
