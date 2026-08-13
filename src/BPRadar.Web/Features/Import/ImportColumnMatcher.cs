namespace BPRadar.Web.Features.Import;

public static class ImportColumnMatcher
{
    public static IReadOnlyDictionary<string, string?> Match(
        IReadOnlyList<string> headers) =>
        ImportColumns.All.ToDictionary(
            column => column.Name,
            column => BestMatch(column, headers),
            StringComparer.Ordinal);

    private static string? BestMatch(
        ImportColumnDefinition column,
        IReadOnlyList<string> headers)
    {
        var candidates = new[] { column.Name }.Concat(column.Aliases)
            .Select(Normalize)
            .Where(value => value.Length > 0)
            .ToArray();
        var scored = headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(header => new
            {
                Header = header,
                Score = candidates.Max(candidate => Similarity(
                    candidate,
                    Normalize(header)))
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Header, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return scored is { Score: >= 0.65 } ? scored.Header : null;
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static double Similarity(string left, string right)
    {
        if (left == right)
        {
            return 1;
        }

        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        if (left.Contains(right, StringComparison.Ordinal) ||
            right.Contains(left, StringComparison.Ordinal))
        {
            return (double)Math.Min(left.Length, right.Length) /
                Math.Max(left.Length, right.Length);
        }

        return 1d - (double)LevenshteinDistance(left, right) /
            Math.Max(left.Length, right.Length);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitution = left[leftIndex - 1] == right[rightIndex - 1]
                    ? 0
                    : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(
                        current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
