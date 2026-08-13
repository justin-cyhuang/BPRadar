using System.Net.Http.Headers;

namespace BPRadar.Web.Features.IssueMatching;

public sealed class GitHubModelsKeywordExtractionService(
    HttpClient httpClient,
    GitHubModelsOptions options) : IKeywordExtractionService
{
    public async Task<IReadOnlyList<string>> ExtractKeywordsAsync(
        string rootCauseText,
        CancellationToken cancellationToken = default)
    {
        using var request = ChatCompletionsKeywordExtraction.CreateRequest(
            options.Endpoint,
            options.Model,
            rootCauseText);

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new InvalidOperationException(
                "IssueMatching:GitHubModels:Token is not configured.");
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Token);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        return await ChatCompletionsKeywordExtraction.ReadKeywordsAsync(
            response,
            "GitHub Models",
            cancellationToken);
    }
}
