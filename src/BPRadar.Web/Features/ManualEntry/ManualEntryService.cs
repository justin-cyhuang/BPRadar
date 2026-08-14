using System.Diagnostics;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Utilities;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.ManualEntry;

public static class ManualEntryService
{
    public const decimal DefaultMinimumScore = 0m;
    public const decimal DefaultMaximumScore = 100m;

    public static async Task<AssessmentChecklist?> GetChecklistAsync(
        BPRadarDbContext dbContext,
        int assessmentId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await dbContext.Assessments
            .AsNoTracking()
            .Include(item => item.Organization)
            .Include(item => item.Framework)
            .ThenInclude(framework => framework.Domains)
            .ThenInclude(domain => domain.Controls)
            .Include(item => item.Results)
            .SingleOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return null;
        }

        var resultByControlId = assessment.Results.ToDictionary(result => result.ControlId);
        var domains = assessment.Framework.Domains
            .OrderBy(domain => domain.SortOrder)
            .ThenBy(domain => domain.Code)
            .Select(domain =>
            {
                var controls = domain.Controls
                    .OrderBy(control => control.SortOrder)
                    .ThenBy(control => control.Code)
                    .Select(control =>
                    {
                        resultByControlId.TryGetValue(control.Id, out var result);
                        return new ChecklistControl(
                            control.Id,
                            control.Code,
                            control.Title,
                            control.Description,
                            result?.Status ?? ComplianceStatus.NotAssessed,
                            result?.Score,
                            result?.Notes,
                            result?.EvidenceUrl);
                    })
                    .ToArray();
                return new ChecklistDomain(
                    domain.Id,
                    domain.Code,
                    domain.Name,
                    UsesNumericScoring: true,
                    DefaultMinimumScore,
                    DefaultMaximumScore,
                    controls,
                    CalculateProgress(controls.Select(control => control.Status)));
            })
            .ToArray();

        return new AssessmentChecklist(
            assessment.Id,
            assessment.Label,
            assessment.Organization.Name,
            assessment.Framework.Name,
            assessment.Framework.Version,
            domains,
            CalculateProgress(domains.SelectMany(domain =>
                domain.Controls.Select(control => control.Status))));
    }

    public static async Task<SaveAssessmentResultOutcome> UpsertAsync(
        BPRadarDbContext dbContext,
        TimeProvider timeProvider,
        int assessmentId,
        int controlId,
        SaveAssessmentResultRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        BPRadarTrace.Write(
            TraceEventType.Start,
            "ManualEntry",
            "AssessmentResultUpsertStarted",
            $"assessmentId={assessmentId} controlId={controlId}");

        var assessment = await dbContext.Assessments
            .Include(item => item.Results)
            .SingleOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            TraceRejected(
                startedAt,
                assessmentId,
                controlId,
                "assessment-not-found");
            return SaveAssessmentResultOutcome.NotFound(
                $"Assessment {assessmentId} does not exist.");
        }

        var control = await dbContext.Controls
            .AsNoTracking()
            .Include(item => item.Domain)
            .SingleOrDefaultAsync(item => item.Id == controlId, cancellationToken);
        if (control is null || control.Domain.FrameworkId != assessment.FrameworkId)
        {
            TraceRejected(
                startedAt,
                assessmentId,
                controlId,
                "control-not-found");
            return SaveAssessmentResultOutcome.NotFound(
                $"Control {controlId} is not part of assessment {assessmentId}.");
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            BPRadarTrace.Write(
                TraceEventType.Warning,
                "ManualEntry",
                "AssessmentResultUpsertRejected",
                $"assessmentId={assessmentId} controlId={controlId} errors={errors.Count}");
            TraceRejected(
                startedAt,
                assessmentId,
                controlId,
                "validation");
            return SaveAssessmentResultOutcome.Invalid(errors);
        }

        var status = Enum.Parse<ComplianceStatus>(request.Status, ignoreCase: true);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = assessment.Results.SingleOrDefault(item => item.ControlId == controlId);
        var created = result is null;
        if (result is null)
        {
            result = new AssessmentResult
            {
                Assessment = assessment,
                ControlId = controlId
            };
            assessment.Results.Add(result);
        }

        result.Status = status;
        result.Score = request.Score;
        result.Notes = TextNormalization.EmptyToNull(request.Notes);
        result.EvidenceUrl = TextNormalization.EmptyToNull(request.EvidenceUrl);
        result.Source = ResultSource.Manual;
        result.UpdatedAt = now;
        assessment.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var progress = await CalculateAssessmentProgressAsync(
            dbContext,
            assessmentId,
            assessment.FrameworkId,
            cancellationToken);
        var duration = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var traceDetails =
            $"assessmentId={assessmentId} controlId={controlId} resultId={result.Id} " +
            $"created={created} durationMs={duration:0.###}";
        BPRadarTrace.Write(
            TraceEventType.Information,
            "ManualEntry",
            "AssessmentResultUpserted",
            traceDetails);
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "ManualEntry",
            "AssessmentResultUpsertCompleted",
            traceDetails);

        return SaveAssessmentResultOutcome.Success(new SavedAssessmentResult(
            result.Id,
            result.AssessmentId,
            result.ControlId,
            result.Status,
            result.Score,
            result.Notes,
            result.EvidenceUrl,
            result.Source,
            result.UpdatedAt,
            progress.Overall,
            progress.Domains.Single(domain => domain.DomainId == control.DomainId)));
    }

    public static ProgressCount CalculateProgress(IEnumerable<ComplianceStatus> statuses)
    {
        var values = statuses.ToArray();
        return new ProgressCount(
            values.Count(status => status != ComplianceStatus.NotAssessed),
            values.Length);
    }

    private static Dictionary<string, string[]> Validate(SaveAssessmentResultRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<ComplianceStatus>(
                request.Status,
                ignoreCase: true,
                out var parsedStatus) ||
            !Enum.IsDefined(parsedStatus))
        {
            errors["Status"] = ["Select a valid compliance status."];
        }

        if (request.Score is < DefaultMinimumScore or > DefaultMaximumScore)
        {
            errors["Score"] =
            [
                $"Score must be between {DefaultMinimumScore:0} and {DefaultMaximumScore:0}."
            ];
        }

        var evidenceUrl = TextNormalization.EmptyToNull(request.EvidenceUrl);
        if (evidenceUrl is not null &&
            (!Uri.TryCreate(evidenceUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            errors["EvidenceUrl"] = ["Evidence URL must be a well-formed HTTP or HTTPS URL."];
        }

        return errors;
    }

    private static async Task<AssessmentProgress> CalculateAssessmentProgressAsync(
        BPRadarDbContext dbContext,
        int assessmentId,
        int frameworkId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Controls
            .AsNoTracking()
            .Where(control => control.Domain.FrameworkId == frameworkId)
            .Select(control => new
            {
                control.DomainId,
                Status = dbContext.AssessmentResults
                    .Where(result =>
                        result.AssessmentId == assessmentId &&
                        result.ControlId == control.Id)
                    .Select(result => (ComplianceStatus?)result.Status)
                    .SingleOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        var domains = rows
            .GroupBy(row => row.DomainId)
            .Select(group => new DomainProgress(
                group.Key,
                group.Count(row => row.Status is not null &&
                    row.Status != ComplianceStatus.NotAssessed),
                group.Count()))
            .ToArray();
        return new AssessmentProgress(
            new ProgressCount(
                domains.Sum(domain => domain.Assessed),
                domains.Sum(domain => domain.Total)),
            domains);
    }

    private static void TraceRejected(
        long startedAt,
        int assessmentId,
        int controlId,
        string reason)
    {
        var duration = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "ManualEntry",
            "AssessmentResultUpsertCompleted",
            $"assessmentId={assessmentId} controlId={controlId} " +
            $"outcome={reason} durationMs={duration:0.###}");
    }
}

public sealed record SaveAssessmentResultRequest(
    string Status,
    decimal? Score,
    string? Notes,
    string? EvidenceUrl);

public sealed record AssessmentChecklist(
    int AssessmentId,
    string Label,
    string OrganizationName,
    string FrameworkName,
    string FrameworkVersion,
    ChecklistDomain[] Domains,
    ProgressCount Progress);

public sealed record ChecklistDomain(
    int Id,
    string Code,
    string Name,
    bool UsesNumericScoring,
    decimal MinimumScore,
    decimal MaximumScore,
    ChecklistControl[] Controls,
    ProgressCount Progress);

public sealed record ChecklistControl(
    int Id,
    string Code,
    string Title,
    string Description,
    ComplianceStatus Status,
    decimal? Score,
    string? Notes,
    string? EvidenceUrl);

public sealed record ProgressCount(int Assessed, int Total);

public sealed record DomainProgress(int DomainId, int Assessed, int Total);

public sealed record AssessmentProgress(
    ProgressCount Overall,
    DomainProgress[] Domains);

public sealed record SavedAssessmentResult(
    int Id,
    int AssessmentId,
    int ControlId,
    ComplianceStatus Status,
    decimal? Score,
    string? Notes,
    string? EvidenceUrl,
    ResultSource Source,
    DateTime UpdatedAt,
    ProgressCount OverallProgress,
    DomainProgress DomainProgress);

public sealed record SaveAssessmentResultOutcome(
    SavedAssessmentResult? Result,
    Dictionary<string, string[]>? Errors,
    string? NotFoundMessage)
{
    public static SaveAssessmentResultOutcome Success(SavedAssessmentResult result) =>
        new(result, null, null);

    public static SaveAssessmentResultOutcome Invalid(Dictionary<string, string[]> errors) =>
        new(null, errors, null);

    public static SaveAssessmentResultOutcome NotFound(string message) =>
        new(null, null, message);
}
