namespace BPRadar.Web.Features.IssueMatching;

public interface IKeywordExtractionService
{
    Task<IReadOnlyList<string>> ExtractKeywordsAsync(
        string rootCauseText,
        CancellationToken cancellationToken = default);
}
