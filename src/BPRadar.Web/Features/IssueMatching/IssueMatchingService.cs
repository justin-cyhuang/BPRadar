using System.Globalization;
using System.Text;

namespace BPRadar.Web.Features.IssueMatching;

public interface IIssueMatchingService
{
    Task<IssueMatchResult> MatchAsync(
        IssueMatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class IssueMatchingService(
    IKeywordExtractionService keywordExtractionService,
    IControlKeywordCatalog controlKeywordCatalog,
    IssueMatchingOptions options) : IIssueMatchingService
{
    public async Task<IssueMatchResult> MatchAsync(
        IssueMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RootCause))
        {
            throw new ArgumentException(
                "Add a Root Cause before running matching.",
                nameof(request));
        }

        var extractedKeywords = (await keywordExtractionService.ExtractKeywordsAsync(
                request.RootCause,
                cancellationToken))
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var controls = await controlKeywordCatalog.GetAllAsync(cancellationToken);

        var candidates = controls
            .Select(control => MatchControl(control, extractedKeywords))
            .Where(candidate => candidate is not null)
            .Cast<ControlMatchCandidate>()
            .OrderByDescending(candidate => candidate.MatchScore)
            .ThenBy(candidate => candidate.FrameworkCode, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ControlCode, StringComparer.Ordinal)
            .ToArray();

        return new IssueMatchResult(extractedKeywords, candidates);
    }

    private ControlMatchCandidate? MatchControl(
        ControlKeywordEntry control,
        IReadOnlyList<string> extractedKeywords)
    {
        var matches =
            from extractedKeyword in extractedKeywords
            from controlKeyword in control.Keywords
            let score = KeywordSimilarity.Score(extractedKeyword, controlKeyword)
            where score >= options.MatchThreshold
            select new
            {
                ExtractedKeyword = extractedKeyword,
                ControlKeyword = controlKeyword,
                Score = score
            };
        var matchedPairs = matches.ToArray();

        if (matchedPairs.Length == 0)
        {
            return null;
        }

        return new ControlMatchCandidate(
            control.FrameworkCode,
            control.ControlCode,
            matchedPairs
                .Select(match => match.ExtractedKeyword)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            matchedPairs
                .Select(match => match.ControlKeyword)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            (decimal)matchedPairs.Max(match => match.Score));
    }

    private static class KeywordSimilarity
    {
        private static readonly HashSet<string> GenericTokens =
        [
            "control",
            "error",
            "fail",
            "failure",
            "gap",
            "issue",
            "missing",
            "problem",
            "process",
            "service",
            "system"
        ];

        public static double Score(string left, string right)
        {
            var normalizedLeft = Normalize(left);
            var normalizedRight = Normalize(right);

            if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
            {
                return 0;
            }

            if (normalizedLeft == normalizedRight)
            {
                return 1;
            }

            var leftTokens = normalizedLeft.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rightTokens = normalizedRight.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commonTokenCount = leftTokens
                .Intersect(rightTokens, StringComparer.Ordinal)
                .Count();
            var tokenScore = (double)commonTokenCount /
                leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
            var significantLeftTokens = leftTokens
                .Where(token => !GenericTokens.Contains(token))
                .ToArray();
            var significantRightTokens = rightTokens
                .Where(token => !GenericTokens.Contains(token))
                .ToArray();
            var significantCommonCount = significantLeftTokens
                .Intersect(significantRightTokens, StringComparer.Ordinal)
                .Count();
            var partialScore =
                significantCommonCount == 0
                    ? 0
                    : 0.9 * significantCommonCount /
                        Math.Min(
                            significantLeftTokens.Length,
                            significantRightTokens.Length);
            var editScore = 1 - (double)LevenshteinDistance(
                normalizedLeft,
                normalizedRight) /
                Math.Max(normalizedLeft.Length, normalizedRight.Length);

            return Math.Max(partialScore, Math.Max(tokenScore, editScore));
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder(value.Length);

            foreach (var character in value.ToLower(CultureInfo.InvariantCulture))
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }

            return string.Join(
                ' ',
                builder
                    .ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Stem));
        }

        private static string Stem(string token)
        {
            if (token.Length > 4 && token.EndsWith("ies", StringComparison.Ordinal))
            {
                return $"{token[..^3]}y";
            }

            foreach (var suffix in new[] { "ing", "ed", "es", "s" })
            {
                if (token.Length > suffix.Length + 3 &&
                    token.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return token[..^suffix.Length];
                }
            }

            return token;
        }

        private static int LevenshteinDistance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];

            for (var index = 0; index <= right.Length; index++)
            {
                previous[index] = index;
            }

            for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
            {
                current[0] = leftIndex;

                for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
                {
                    var substitutionCost =
                        left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                    current[rightIndex] = Math.Min(
                        Math.Min(
                            current[rightIndex - 1] + 1,
                            previous[rightIndex] + 1),
                        previous[rightIndex - 1] + substitutionCost);
                }

                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }
    }
}
