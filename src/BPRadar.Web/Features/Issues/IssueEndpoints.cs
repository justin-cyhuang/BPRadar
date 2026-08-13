using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.IssueMatching;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace BPRadar.Web.Features.Issues;

public static class IssueEndpoints
{
    public static IEndpointRouteBuilder MapIssueEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/organizations/{organizationId:int}/issues",
            CreateAsync);
        endpoints.MapGet("/api/issues/{issueId:int}", GetAsync);
        endpoints.MapPut("/api/issues/{issueId:int}", UpdateAsync);
        endpoints.MapGet(
            "/api/organizations/{organizationId:int}/issues",
            ListAsync);
        endpoints.MapPost("/api/issues/{issueId:int}/matching", RunMatchingAsync);
        endpoints.MapPut(
            "/api/violation-matches/{violationMatchId:int}/review",
            ReviewAsync);
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(
        int issueId,
        CreateIssueRequest request,
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        WriteOperationTrace(
            TraceEventType.Start,
            "IssueUpdateStarted",
            $"IssueId={issueId}");
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            WriteOperationTrace(
                TraceEventType.Warning,
                "IssueUpdateRejected",
                $"IssueId={issueId}",
                timer.ElapsedMilliseconds);
            return ValidationProblem(errors);
        }

        var issue = await dbContext.Issues
            .Include(item => item.ViolationMatches)
            .ThenInclude(match => match.Control)
            .SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken);
        if (issue is null)
        {
            WriteOperationTrace(
                TraceEventType.Warning,
                "IssueUpdateNotFound",
                $"IssueId={issueId}",
                timer.ElapsedMilliseconds);
            return NotFoundProblem("Issue was not found.");
        }

        issue.Title = request.Title.Trim();
        issue.Description = request.Description.Trim();
        issue.RootCause = request.RootCause?.Trim() ?? string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteOperationTrace(
            TraceEventType.Stop,
            "IssueUpdateSucceeded",
            $"IssueId={issueId} OrganizationId={issue.OrganizationId}",
            timer.ElapsedMilliseconds);
        return Results.Ok(await ToDetailAsync(dbContext, issue, cancellationToken));
    }

    private static async Task<IResult> ListAsync(
        int organizationId,
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Organizations.AnyAsync(
                organization => organization.Id == organizationId,
                cancellationToken))
        {
            return NotFoundProblem("Organization was not found.");
        }

        var issues = await dbContext.Issues
            .AsNoTracking()
            .Where(issue => issue.OrganizationId == organizationId)
            .Include(issue => issue.ViolationMatches)
            .ThenInclude(match => match.Control)
            .OrderByDescending(issue => issue.CreatedAt)
            .ToListAsync(cancellationToken);
        var details = new List<IssueDetail>(issues.Count);
        foreach (var issue in issues)
        {
            details.Add(await ToDetailAsync(dbContext, issue, cancellationToken));
        }

        return Results.Ok(details);
    }

    private static async Task<IResult> GetAsync(
        int issueId,
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.Issues
            .AsNoTracking()
            .Include(item => item.ViolationMatches)
            .ThenInclude(match => match.Control)
            .SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken);
        return issue is null
            ? NotFoundProblem("Issue was not found.")
            : Results.Ok(await ToDetailAsync(dbContext, issue, cancellationToken));
    }

    private static async Task<IResult> CreateAsync(
        int organizationId,
        CreateIssueRequest request,
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        WriteOperationTrace(
            TraceEventType.Start,
            "IssueCreateStarted",
            $"OrganizationId={organizationId}");
        if (!await dbContext.Organizations.AnyAsync(
                organization => organization.Id == organizationId,
                cancellationToken))
        {
            WriteOperationTrace(
                TraceEventType.Warning,
                "IssueCreateRejected",
                $"OrganizationId={organizationId}",
                timer.ElapsedMilliseconds);
            return ValidationProblem(new Dictionary<string, string[]>
            {
                ["OrganizationId"] = [$"Organization {organizationId} does not exist."]
            });
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            WriteOperationTrace(
                TraceEventType.Warning,
                "IssueCreateRejected",
                $"OrganizationId={organizationId}",
                timer.ElapsedMilliseconds);
            return ValidationProblem(errors);
        }

        var issue = new Issue
        {
            OrganizationId = organizationId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            RootCause = request.RootCause?.Trim() ?? string.Empty,
            MatchingStatus = IssueMatchingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Issues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);

        WriteOperationTrace(
            TraceEventType.Stop,
            "IssueCreated",
            $"IssueId={issue.Id} OrganizationId={organizationId}",
            timer.ElapsedMilliseconds);

        return Results.Created(
            $"/api/organizations/{organizationId}/issues/{issue.Id}",
            ToDetail(issue));
    }

    private static Dictionary<string, string[]> Validate(CreateIssueRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["Title"] = ["A title is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["Description"] = ["A description is required."];
        }

        return errors;
    }

    private static IssueDetail ToDetail(Issue issue) =>
        new(
            issue.Id,
            issue.OrganizationId,
            issue.Title,
            issue.Description,
            issue.RootCause,
            issue.MatchingStatus.ToString(),
            issue.MatchingError,
            issue.CreatedAt,
            issue.MatchedAt,
            []);

private static async Task<IResult> RunMatchingAsync(
    int issueId,
    BPRadarDbContext dbContext,
    IIssueMatchingService matchingService,
    CancellationToken cancellationToken)
{
    var issue = await dbContext.Issues
        .Include(item => item.ViolationMatches)
        .ThenInclude(match => match.Control)
        .SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken);
    if (issue is null)
    {
        return NotFoundProblem("Issue was not found.");
    }

    if (string.IsNullOrWhiteSpace(issue.RootCause))
    {
        return ValidationProblem(new Dictionary<string, string[]>
        {
            ["RootCause"] = ["Add a Root Cause before running matching."]
        });
    }

    issue.MatchingStatus = IssueMatchingStatus.Pending;
    issue.MatchingError = null;
    issue.MatchedAt = null;
    await dbContext.SaveChangesAsync(cancellationToken);

    var timer = Stopwatch.StartNew();
    WriteMatchingTrace(
        TraceEventType.Start,
        "IssueMatchingStarted",
        issue.Id,
        $"RootCauseLength={issue.RootCause.Length}");

    try
    {
        var result = await matchingService.MatchAsync(
            new IssueMatchRequest(issue.Description, issue.RootCause),
            cancellationToken);
        var candidateCodes = result.Candidates
            .Select(candidate => candidate.ControlCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var controls = await dbContext.Controls
            .Where(control => candidateCodes.Contains(control.Code))
            .ToArrayAsync(cancellationToken);
        var controlsByCode = controls.ToDictionary(
            control => control.Code,
            StringComparer.OrdinalIgnoreCase);
        var unmatchedCandidates = result.Candidates
            .Where(candidate => !controlsByCode.ContainsKey(candidate.ControlCode))
            .Select(candidate =>
                $"'{candidate.FrameworkCode}/{candidate.ControlCode}'")
            .ToArray();
        if (unmatchedCandidates.Length > 0)
        {
            throw new InvalidDataException(
                $"Matched controls do not exist: {string.Join(", ", unmatchedCandidates)}.");
        }

        var selfReportedStates = new Dictionary<int, SurveyResponseLevel?>();
        foreach (var control in controls)
        {
            selfReportedStates[control.Id] = await LatestSurveyResponseAsync(
                dbContext,
                issue.OrganizationId,
                control.Id,
                cancellationToken);
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var candidateControlIds = controlsByCode.Values
            .Select(control => control.Id)
            .ToHashSet();
        var staleOpenMatches = issue.ViolationMatches
            .Where(match =>
                match.ReviewStatus == ViolationMatchReviewStatus.Open &&
                !candidateControlIds.Contains(match.ControlId))
            .ToArray();
        dbContext.ViolationMatches.RemoveRange(staleOpenMatches);

        foreach (var candidate in result.Candidates)
        {
            var control = controlsByCode[candidate.ControlCode];
            var selfReportedState = selfReportedStates[control.Id];
            var match = issue.ViolationMatches.SingleOrDefault(
                existing => existing.ControlId == control.Id);
            if (match is null)
            {
                match = new ViolationMatch
                {
                    Control = control,
                    MatchedKeywords = "[]",
                    ReviewStatus = ViolationMatchReviewStatus.Open,
                    CreatedAt = DateTime.UtcNow
                };
                issue.ViolationMatches.Add(match);
            }

            match.MatchedKeywords = JsonSerializer.Serialize(candidate.MatchedKeywords);
            match.MatchScore = candidate.MatchScore;
            match.IsSelfAssessmentDiscrepancy =
                selfReportedState is SurveyResponseLevel.High or
                    SurveyResponseLevel.VeryHigh;
        }

        issue.MatchingStatus = IssueMatchingStatus.Matched;
        issue.MatchedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        WriteMatchingTrace(
            TraceEventType.Stop,
            "IssueMatchingSucceeded",
            issue.Id,
            $"MatchCount={result.Candidates.Count}",
            timer.ElapsedMilliseconds);

        return Results.Ok(await ToDetailAsync(
            dbContext,
            issue,
            cancellationToken));
    }
    catch (Exception exception) when (IsMatchingFailure(exception))
    {
        dbContext.ChangeTracker.Clear();
        issue = await dbContext.Issues.SingleAsync(
            item => item.Id == issueId,
            CancellationToken.None);
        issue.MatchingStatus = IssueMatchingStatus.Failed;
        issue.MatchingError = ShortError(exception);
        issue.MatchedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        WriteMatchingTrace(
            TraceEventType.Error,
            "IssueMatchingFailed",
            issue.Id,
            $"Error={ShortError(exception)}",
            timer.ElapsedMilliseconds);

        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Issue matching failed.",
            detail: issue.MatchingError,
            extensions: CorrelationExtensions());
    }
}

private static async Task<IResult> ReviewAsync(
    int violationMatchId,
    ReviewViolationMatchRequest request,
    BPRadarDbContext dbContext,
    CancellationToken cancellationToken)
{
    var timer = Stopwatch.StartNew();
    WriteOperationTrace(
        TraceEventType.Start,
        "ViolationMatchReviewStarted",
        $"ViolationMatchId={violationMatchId}");
    if (!Enum.TryParse<ViolationMatchReviewStatus>(
            request.ReviewStatus,
            ignoreCase: true,
            out var reviewStatus) ||
        !Enum.IsDefined(reviewStatus) ||
        int.TryParse(request.ReviewStatus, out _))
    {
        WriteOperationTrace(
            TraceEventType.Warning,
            "ViolationMatchReviewRejected",
            $"ViolationMatchId={violationMatchId}",
            timer.ElapsedMilliseconds);
        return ValidationProblem(new Dictionary<string, string[]>
        {
            ["ReviewStatus"] =
            [
                "Review status must be Open, Confirmed, or Dismissed."
            ]
        });
    }

    var match = await dbContext.ViolationMatches
        .Include(item => item.Control)
        .Include(item => item.Issue)
        .SingleOrDefaultAsync(
            item => item.Id == violationMatchId,
            cancellationToken);
    if (match is null)
    {
        WriteOperationTrace(
            TraceEventType.Warning,
            "ViolationMatchReviewNotFound",
            $"ViolationMatchId={violationMatchId}",
            timer.ElapsedMilliseconds);
        return NotFoundProblem("Violation Match was not found.");
    }

    match.ReviewStatus = reviewStatus;
    await dbContext.SaveChangesAsync(cancellationToken);
    WriteOperationTrace(
        TraceEventType.Stop,
        "ViolationMatchReviewed",
        $"ViolationMatchId={match.Id} IssueId={match.IssueId} " +
        $"ControlId={match.ControlId} ReviewStatus={reviewStatus}",
        timer.ElapsedMilliseconds);

    return Results.Ok(await ToMatchDetailAsync(
        dbContext,
        match.Issue.OrganizationId,
        match,
        cancellationToken));
}

private static async Task<SurveyResponseLevel?> LatestSurveyResponseAsync(
    BPRadarDbContext dbContext,
    int organizationId,
    int controlId,
    CancellationToken cancellationToken) =>
    await dbContext.SurveyResponses
        .Where(response =>
            response.SurveySubmission.OrganizationId == organizationId &&
            response.SurveyQuestion.ControlId == controlId)
        .OrderByDescending(response => response.SurveySubmission.SnapshotDate)
        .ThenByDescending(response => response.SurveySubmission.SubmittedAt)
        .Select(response => (SurveyResponseLevel?)response.ResponseLevel)
        .FirstOrDefaultAsync(cancellationToken);

internal static async Task<IssueDetail> ToDetailAsync(
    BPRadarDbContext dbContext,
    Issue issue,
    CancellationToken cancellationToken)
{
    var details = new List<ViolationMatchDetail>();
    foreach (var match in issue.ViolationMatches)
    {
        details.Add(await ToMatchDetailAsync(
            dbContext,
            issue.OrganizationId,
            match,
            cancellationToken));
    }

    return new IssueDetail(
        issue.Id,
        issue.OrganizationId,
        issue.Title,
        issue.Description,
        issue.RootCause,
        issue.MatchingStatus.ToString(),
        issue.MatchingError,
        issue.CreatedAt,
        issue.MatchedAt,
        details
            .OrderByDescending(match => match.IsSelfAssessmentDiscrepancy)
            .ThenBy(match => match.ReviewStatus == "Dismissed")
            .ThenByDescending(match => match.MatchScore)
            .ToArray());
}

private static async Task<ViolationMatchDetail> ToMatchDetailAsync(
    BPRadarDbContext dbContext,
    int organizationId,
    ViolationMatch match,
    CancellationToken cancellationToken)
{
    var selfReportedState = await LatestSurveyResponseAsync(
        dbContext,
        organizationId,
        match.ControlId,
        cancellationToken);
    var isSelfAssessmentDiscrepancy =
        selfReportedState is SurveyResponseLevel.High or
            SurveyResponseLevel.VeryHigh;
    return new ViolationMatchDetail(
        match.Id,
        match.ControlId,
        match.Control.Code,
        match.Control.Title,
        match.Control.Description,
        match.Control.GuidanceUrl,
        JsonSerializer.Deserialize<string[]>(match.MatchedKeywords) ?? [],
        match.MatchScore,
        match.ReviewStatus.ToString(),
        isSelfAssessmentDiscrepancy,
        selfReportedState is null
            ? "NoSurveyResponse"
            : isSelfAssessmentDiscrepancy
                ? "Discrepancy"
                : "NoDiscrepancy",
        selfReportedState?.ToString());
}

private static bool IsMatchingFailure(Exception exception) =>
    exception is HttpRequestException or
        ArgumentException or
        IOException or
        InvalidDataException or
        InvalidOperationException or
        DbUpdateException or
        TaskCanceledException;

private static string ShortError(Exception exception)
{
    const int maximumLength = 500;
    var message = exception.Message.Replace('\r', ' ').Replace('\n', ' ');
    return message.Length <= maximumLength
        ? message
        : message[..maximumLength];
}

private static void WriteMatchingTrace(
    TraceEventType severity,
    string operation,
    int issueId,
    string details,
    long? durationMilliseconds = null) =>
    BPRadarTrace.Write(
        severity,
        "Issues",
        operation,
        $"IssueId={issueId} {details}",
        durationMilliseconds);

    private static void WriteOperationTrace(
        TraceEventType severity,
        string operation,
        string details,
        long? durationMilliseconds = null) =>
        BPRadarTrace.Write(
        severity,
        "Issues",
        operation,
        details,
        durationMilliseconds);

    private static IResult ValidationProblem(
        Dictionary<string, string[]> errors) =>
        Results.ValidationProblem(
        errors,
        extensions: CorrelationExtensions());

    private static IResult NotFoundProblem(string detail) =>
        Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Resource not found.",
        detail: detail,
        extensions: CorrelationExtensions());

    private static Dictionary<string, object?> CorrelationExtensions() =>
        new()
        {
        ["correlationId"] = BPRadarTrace.CorrelationId
        };
}

public sealed record CreateIssueRequest(
    string Title,
    string Description,
    string? RootCause);

public sealed record ReviewViolationMatchRequest(string ReviewStatus);

public sealed record IssueDetail(
    int Id,
    int OrganizationId,
    string Title,
    string Description,
    string RootCause,
    string MatchingStatus,
    string? MatchingError,
    DateTime CreatedAt,
    DateTime? MatchedAt,
    ViolationMatchDetail[] ViolationMatches);

public sealed record ViolationMatchDetail(
    int Id,
    int ControlId,
    string ControlCode,
    string ControlTitle,
    string ControlDescription,
    string? GuidanceUrl,
    string[] MatchedKeywords,
    decimal MatchScore,
    string ReviewStatus,
    bool IsSelfAssessmentDiscrepancy,
    string DiscrepancyStatus,
    string? SelfReportedState);
