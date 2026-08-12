using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BPRadar.Web.Features.IssueMatching;

public sealed class GitHubModelsKeywordExtractionService(
    HttpClient httpClient,
    GitHubModelsOptions options) : IKeywordExtractionService
{
    private const string SystemPrompt =
        """
        Extract 3 to 8 concise control-failure keywords or short phrases from the
        supplied Root Cause. Return only a JSON object with a "keywords" array.
        Focus on violated operational or governance practices, not product names,
        people, or incident-specific identifiers.
        """;

    public async Task<IReadOnlyList<string>> ExtractKeywordsAsync(
        string rootCauseText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootCauseText))
        {
            throw new ArgumentException(
                "Root Cause text is required.",
                nameof(rootCauseText));
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new InvalidOperationException(
                "IssueMatching:GitHubModels:Token is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = options.Model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = rootCauseText }
                },
                temperature = 0,
                response_format = new { type = "json_object" }
            })
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Token);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub Models keyword extraction failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        ChatCompletion? completion;

        try
        {
            completion = await response.Content.ReadFromJsonAsync<ChatCompletion>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "GitHub Models returned an invalid completion response.",
                exception);
        }

        var modelContent = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(modelContent))
        {
            throw new InvalidDataException(
                "GitHub Models returned no keyword extraction content.");
        }

        KeywordExtraction? extraction;

        try
        {
            extraction = JsonSerializer.Deserialize<KeywordExtraction>(
                RemoveMarkdownFence(modelContent));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "GitHub Models returned invalid keyword extraction content.",
                exception);
        }

        if (extraction?.Keywords is null)
        {
            throw new InvalidDataException(
                "GitHub Models returned an invalid keyword extraction response.");
        }

        return extraction.Keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string RemoveMarkdownFence(string content)
    {
        var trimmed = content.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return firstLineEnd >= 0 && closingFence > firstLineEnd
            ? trimmed[(firstLineEnd + 1)..closingFence].Trim()
            : trimmed;
    }

    private sealed record ChatCompletion(
        [property: JsonPropertyName("choices")] IReadOnlyList<Choice>? Choices);

    private sealed record Choice(
        [property: JsonPropertyName("message")] Message? Message);

    private sealed record Message(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record KeywordExtraction(
        [property: JsonPropertyName("keywords")] IReadOnlyList<string>? Keywords);
}
