using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BPRadar.Web.Authentication;

internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    IOptionsMonitor<ApiKeyOptions> apiOptions,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        schemeOptions,
        logger,
        encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var options = apiOptions.CurrentValue;
        if (!options.RequireApiKey)
        {
            return Task.FromResult(Succeed());
        }

        if (options.ApiKey is null ||
            !Request.Headers.TryGetValue(
                ApiKeyAuthenticationDefaults.HeaderName,
                out var values) ||
            values.Count != 1 ||
            string.IsNullOrEmpty(values[0]) ||
            !KeysMatch(options.ApiKey, values[0]!))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("A valid API key is required."));
        }

        return Task.FromResult(Succeed());
    }

    private AuthenticateResult Succeed()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(
                    ClaimTypes.NameIdentifier,
                    ApiKeyAuthenticationDefaults.SchemeName)
            ],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name));
    }

    private static bool KeysMatch(string expected, string presented)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        return CryptographicOperations.FixedTimeEquals(
            expectedBytes,
            presentedBytes);
    }
}
