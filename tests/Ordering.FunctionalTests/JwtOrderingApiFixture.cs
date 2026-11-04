using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace eShop.Ordering.FunctionalTests;

/// <summary>
/// Ordering.API with its real JWT bearer validation (ServiceDefaults) instead of AutoAuthorizeMiddleware. Only
/// Identity.API's discovery document is replaced: the signing key and issuer come from here, so tests can mint the
/// tokens Identity.API would issue.
/// </summary>
public sealed class JwtOrderingApiFixture : OrderingApiFixture
{
    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "test" };

    protected override void ConfigureAuthentication(IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = IdentityUrl };
            configuration.SigningKeys.Add(_signingKey);
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        });
    }

    /// <summary>An access token from Identity.API for <paramref name="audience"/> with <paramref name="scope"/>.</summary>
    public string MintToken(string audience, string scope) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "at+jwt",
            Issuer = IdentityUrl,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = AutoAuthorizeMiddleware.IDENTITY_ID,
                ["aud"] = audience,
                ["scope"] = scope
            },
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        });
}
