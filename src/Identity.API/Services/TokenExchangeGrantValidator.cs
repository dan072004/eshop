namespace eShop.Identity.API.Services;

/// <summary>
/// RFC 8693 token exchange as delegation (ADR 0005): eShop.Mcp (client <c>eshop-mcp-server</c>, the only client
/// allowed this grant) trades a Buyer's eShop.Mcp token for one Ordering.API accepts. The subject token must be an
/// unexpired access token Identity.API issued, for eShop.Mcp's URL as audience and with the <c>eshop-mcp</c> scope.
/// The issued token is for the same <c>sub</c>, carries <c>act: { client_id }</c>, and gets its scope (<c>orders</c>)
/// and lifetime from the client. The profile service still refuses a Buyer who doesn't exist or isn't active.
/// </summary>
public sealed class TokenExchangeGrantValidator(ITokenValidator tokenValidator, IConfiguration configuration)
    : IExtensionGrantValidator
{
    private const string EshopMcpScope = "eshop-mcp";

    private readonly string _eshopMcpUrl = configuration.GetRequiredValue("EshopMcpUrl");

    public string GrantType => OidcConstants.GrantTypes.TokenExchange;

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var form = context.Request.Raw;
        var subjectToken = form[OidcConstants.TokenRequest.SubjectToken];
        if (string.IsNullOrEmpty(subjectToken)
            || form[OidcConstants.TokenRequest.SubjectTokenType] != OidcConstants.TokenTypeIdentifiers.AccessToken)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest);
            return;
        }

        // Checks the signature against Identity.API's keys, the issuer, the lifetime and the eshop-mcp scope.
        // It does not check the audience, so that is done here.
        var validation = await tokenValidator.ValidateAccessTokenAsync(subjectToken, EshopMcpScope);
        var subject = validation.Claims?.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject)?.Value;
        if (validation.IsError
            || string.IsNullOrEmpty(subject)
            || !validation.Claims!.Any(c => c.Type == JwtClaimTypes.Audience && c.Value == _eshopMcpUrl))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var actor = new Claim(
            JwtClaimTypes.Actor,
            System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [JwtClaimTypes.ClientId] = context.Request.Client.ClientId
            }),
            IdentityServerConstants.ClaimValueTypes.Json);
        context.Result = new GrantValidationResult(subject, GrantType, [actor]);
    }
}
