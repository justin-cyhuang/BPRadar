using System.Diagnostics;
using BPRadar.Web.Diagnostics;

namespace BPRadar.Web.Features.IssueMatching;

public static class IssueMatchingEndpoint
{
    public static async Task<IResult> MatchCandidatesAsync(
        IssueMatchRequest request,
        IIssueMatchingService matchingService,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        WriteTrace(
            TraceEventType.Information,
            "IssueMatchingStarted",
            $"RootCauseLength={request.RootCause?.Length ?? 0}");

        try
        {
            var result = await matchingService.MatchAsync(request, cancellationToken);
            WriteTrace(
                TraceEventType.Information,
                "IssueMatchingSucceeded",
                $"DurationMs={stopwatch.ElapsedMilliseconds} MatchCount={result.Candidates.Count}");

            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            WriteTrace(
                TraceEventType.Warning,
                "IssueMatchingFailed",
                $"DurationMs={stopwatch.ElapsedMilliseconds} MatchCount=0 Error={Sanitize(exception.Message)}");

            return Results.BadRequest(new
            {
                error = exception.Message,
                correlationId = BPRadarTrace.CorrelationId
            });
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
                InvalidDataException or
                InvalidOperationException or
                TaskCanceledException)
        {
            WriteTrace(
                TraceEventType.Error,
                "IssueMatchingFailed",
                $"DurationMs={stopwatch.ElapsedMilliseconds} MatchCount=0 Error={Sanitize(exception.Message)}");

            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Keyword extraction failed.",
                detail: "The configured keyword extraction provider could not complete the request.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["correlationId"] = BPRadarTrace.CorrelationId
                    });
        }
    }

    private static void WriteTrace(
        TraceEventType severity,
        string operation,
        string details)
    {
        BPRadarTrace.Write(
                severity,
                "IssueMatching",
                operation,
                details);
    }

    private static string Sanitize(string value)
    {
        return value
                .Replace('\r', ' ')
                .Replace('\n', ' ');
    }
}
