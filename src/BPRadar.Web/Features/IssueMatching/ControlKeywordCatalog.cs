using System.Text.Json;
using System.Text.Json.Serialization;

namespace BPRadar.Web.Features.IssueMatching;

public interface IControlKeywordCatalog
{
    Task<IReadOnlyList<ControlKeywordEntry>> GetAllAsync(
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryControlKeywordCatalog(
    IReadOnlyList<ControlKeywordEntry> entries) : IControlKeywordCatalog
{
    public Task<IReadOnlyList<ControlKeywordEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(entries);
    }
}

public sealed class JsonControlKeywordCatalog(string path) : IControlKeywordCatalog
{
    public async Task<IReadOnlyList<ControlKeywordEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ControlKeywordDocument>(
            stream,
            cancellationToken: cancellationToken);

        if (document?.Frameworks is null)
        {
            throw new InvalidDataException(
                $"Control Keyword seed data at '{path}' is invalid.");
        }

        return document.Frameworks
            .SelectMany(
                framework => framework.Controls.Select(
                    control => new ControlKeywordEntry(
                        framework.FrameworkCode,
                        control.ControlCode,
                        control.Keywords)))
            .ToArray();
    }

    private sealed record ControlKeywordDocument(
        [property: JsonPropertyName("frameworks")]
        IReadOnlyList<FrameworkKeywords> Frameworks);

    private sealed record FrameworkKeywords(
        [property: JsonPropertyName("frameworkCode")] string FrameworkCode,
        [property: JsonPropertyName("controls")]
        IReadOnlyList<ControlKeywords> Controls);

    private sealed record ControlKeywords(
        [property: JsonPropertyName("controlCode")] string ControlCode,
        [property: JsonPropertyName("keywords")]
        IReadOnlyList<string> Keywords);
}
