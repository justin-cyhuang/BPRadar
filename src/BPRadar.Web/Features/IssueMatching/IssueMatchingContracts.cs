namespace BPRadar.Web.Features.IssueMatching;

public sealed record IssueMatchRequest(string Description, string RootCause);

public sealed record IssueMatchResult(
    IReadOnlyList<string> ExtractedKeywords,
    IReadOnlyList<ControlMatchCandidate> Candidates);

public sealed record ControlMatchCandidate(
    string FrameworkCode,
    string ControlCode,
    IReadOnlyList<string> MatchedKeywords,
    IReadOnlyList<string> MatchedControlKeywords,
    decimal MatchScore);

public sealed record ControlKeywordEntry(
    string FrameworkCode,
    string ControlCode,
    IReadOnlyList<string> Keywords);
