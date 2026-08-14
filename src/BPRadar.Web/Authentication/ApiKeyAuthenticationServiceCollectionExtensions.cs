using Microsoft.AspNetCore.Authentication;

namespace BPRadar.Web.Authentication;

internal static class ApiKeyAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .Validate(
                options =>
                    !options.RequireApiKey ||
                    !string.IsNullOrWhiteSpace(options.ApiKey),
                "Api:ApiKey must be configured when Api:RequireApiKey is true.")
            .ValidateOnStart();

        services
            .AddAuthentication(ApiKeyAuthenticationDefaults.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.SchemeName,
                _ => { });
        services.AddAuthorization();
        return services;
    }
}
