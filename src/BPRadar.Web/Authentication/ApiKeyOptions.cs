namespace BPRadar.Web.Authentication;

internal sealed class ApiKeyOptions
{
    public const string SectionName = "Api";

    public bool RequireApiKey { get; init; }

    public string? ApiKey { get; init; }
}
