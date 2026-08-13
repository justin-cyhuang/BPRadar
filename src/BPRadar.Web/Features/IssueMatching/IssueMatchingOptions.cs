namespace BPRadar.Web.Features.IssueMatching;

public sealed class IssueMatchingOptions
{
    public const string SectionName = "IssueMatching";

    public string LlmProvider { get; set; } = "GitHubModels";

    public double MatchThreshold { get; set; } = 0.72;

    public string ControlKeywordSeedPath { get; set; } =
        "seed-data/control-keywords.json";

    public GitHubModelsOptions GitHubModels { get; set; } = new();

    public OpenAICompatibleOptions OpenAICompatible { get; set; } = new();
}

public sealed class GitHubModelsOptions
{
    public string Endpoint { get; set; } =
        "https://models.github.ai/inference/chat/completions";

    public string Model { get; set; } = "openai/gpt-4.1-mini";

    public string? Token { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class OpenAICompatibleOptions
{
    public string Endpoint { get; set; } =
        "https://api.openai.com/v1/chat/completions";

    public string Model { get; set; } = "gpt-4.1-mini";

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public string ApiKeyHeaderName { get; set; } = "Authorization";

    public string? AuthScheme { get; set; } = "Bearer";
}
