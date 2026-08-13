namespace BPRadar.Web.Features.IssueMatching;

public sealed class OpenAICompatibleKeywordExtractionService(
    HttpClient httpClient,
    OpenAICompatibleOptions options) : IKeywordExtractionService
{
    public async Task<IReadOnlyList<string>> ExtractKeywordsAsync(
        string rootCauseText,
        CancellationToken cancellationToken = default)
    {
        using var request = ChatCompletionsKeywordExtraction.CreateRequest(
            options.Endpoint,
            options.Model,
            rootCauseText);

        AddAuthenticationHeader(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        return await ChatCompletionsKeywordExtraction.ReadKeywordsAsync(
            response,
            "OpenAI-compatible provider",
            cancellationToken);
    }

    private void AddAuthenticationHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKeyHeaderName))
        {
            throw new InvalidOperationException(
                "IssueMatching:OpenAICompatible:ApiKeyHeaderName is required when ApiKey is configured.");
        }

        if (string.Equals(
                options.ApiKeyHeaderName,
                "Authorization",
                StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.AuthScheme))
        {
            throw new InvalidOperationException(
                "IssueMatching:OpenAICompatible:AuthScheme is required for the Authorization header.");
        }

        var headerValue = string.IsNullOrWhiteSpace(options.AuthScheme)
            ? options.ApiKey
            : $"{options.AuthScheme.Trim()} {options.ApiKey}";

        if (!request.Headers.TryAddWithoutValidation(
                options.ApiKeyHeaderName,
                headerValue))
        {
            throw new InvalidOperationException(
                $"IssueMatching:OpenAICompatible:ApiKeyHeaderName '{options.ApiKeyHeaderName}' is invalid.");
        }
    }
}
