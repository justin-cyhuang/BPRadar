namespace BPRadar.Web.Features.IssueMatching;

public static class IssueMatchingServiceCollectionExtensions
{
    public static IServiceCollection AddIssueMatching(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var options = configuration
            .GetSection(IssueMatchingOptions.SectionName)
            .Get<IssueMatchingOptions>() ?? new IssueMatchingOptions();

        if (options.MatchThreshold is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "IssueMatching:MatchThreshold must be between 0 and 1.");
        }

        var seedPath = ResolveSeedPath(
            options.ControlKeywordSeedPath,
            contentRootPath);

        services.AddSingleton(options);
        services.AddSingleton(options.GitHubModels);
        services.AddSingleton<IControlKeywordCatalog>(
            new JsonControlKeywordCatalog(seedPath));
        services.AddTransient<IIssueMatchingService, IssueMatchingService>();

        switch (options.LlmProvider.Trim())
        {
            case "GitHubModels":
                services
                    .AddHttpClient<GitHubModelsKeywordExtractionService>(
                        client =>
                        {
                            client.Timeout = TimeSpan.FromSeconds(
                                options.GitHubModels.TimeoutSeconds);
                        });
                services.AddTransient<IKeywordExtractionService>(
                    provider => provider.GetRequiredService<
                        GitHubModelsKeywordExtractionService>());
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported IssueMatching LLM provider '{options.LlmProvider}'.");
        }

        return services;
    }

    private static string ResolveSeedPath(
        string configuredPath,
        string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var contentPath = Path.Combine(contentRootPath, configuredPath);

        return File.Exists(contentPath)
            ? contentPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
    }
}
