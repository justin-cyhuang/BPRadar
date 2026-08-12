using BPRadar.Web.Features.IssueMatching;

namespace BPRadar.Tests.Features.IssueMatching;

[TestClass]
public sealed class IssueMatchingServiceTests
{
    [TestMethod]
    public async Task MatchAsync_ReturnsRankedControlCandidates()
    {
        var extractor = new StubKeywordExtractionService(
            ["single point failure", "missing redundancy"]);
        var catalog = new InMemoryControlKeywordCatalog(
        [
            new("AZURE_WAF", "RE:03", ["single point of failure"]),
            new("AZURE_WAF", "RE:05", ["missing redundancy"]),
            new("AZURE_WAF", "SE:09", ["secret rotation failure"])
        ]);
        var service = new IssueMatchingService(
            extractor,
            catalog,
            new IssueMatchingOptions { MatchThreshold = 0.70 });

        var result = await service.MatchAsync(
            new IssueMatchRequest(
                "Checkout stopped serving traffic.",
                "A single point failure existed because redundancy was missing."));

        CollectionAssert.AreEqual(
            new[] { "RE:05", "RE:03" },
            result.Candidates.Select(candidate => candidate.ControlCode).ToArray());
        Assert.AreEqual(1.0m, result.Candidates[0].MatchScore);
        CollectionAssert.AreEqual(
            new[] { "single point failure", "missing redundancy" },
            result.ExtractedKeywords.ToArray());
    }

    [TestMethod]
    public async Task MatchAsync_MatchesStemmedPartialKeyword()
    {
        var service = new IssueMatchingService(
            new StubKeywordExtractionService(["backups"]),
            new InMemoryControlKeywordCatalog(
            [
                new("ISO27001_2022", "A.8.13", ["backup failure"]),
                new("AZURE_WAF", "RE:07", ["cascading failure"])
            ]),
            new IssueMatchingOptions { MatchThreshold = 0.70 });

        var result = await service.MatchAsync(
            new IssueMatchRequest(
                "Records were unavailable.",
                "Scheduled backups stopped running."));

        Assert.HasCount(1, result.Candidates);
        Assert.AreEqual("A.8.13", result.Candidates[0].ControlCode);
        Assert.IsGreaterThanOrEqualTo(
            0.80m,
            result.Candidates[0].MatchScore);
    }

    private sealed class StubKeywordExtractionService(
        IReadOnlyList<string> keywords) : IKeywordExtractionService
    {
        public Task<IReadOnlyList<string>> ExtractKeywordsAsync(
            string rootCauseText,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(keywords);
        }
    }
}
