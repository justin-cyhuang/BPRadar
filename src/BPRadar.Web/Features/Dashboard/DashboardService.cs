using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Dashboard;

public static class DashboardService
{
    public static async Task<DashboardView> GetAsync(
        BPRadarDbContext dbContext,
        DashboardRequest request,
        CancellationToken cancellationToken = default)
    {
        var assessmentScope = await dbContext.Assessments
            .AsNoTracking()
            .Where(assessment => assessment.OrganizationId == request.OrganizationId)
            .OrderBy(assessment => assessment.Framework.Name)
            .ThenBy(assessment => assessment.Framework.Version)
            .ThenByDescending(assessment => assessment.SnapshotDate)
            .ThenByDescending(assessment => assessment.UpdatedAt)
            .Select(assessment => new
            {
                assessment.Id,
                assessment.FrameworkId,
                FrameworkName = assessment.Framework.Name,
                FrameworkVersion = assessment.Framework.Version,
                assessment.Label,
                assessment.SnapshotDate,
                assessment.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);
        var validAssessmentIds = assessmentScope
            .Select(assessment => assessment.Id)
            .ToHashSet();
        var assessmentIds = request.AssessmentIds
            .Where(validAssessmentIds.Contains)
            .Distinct()
            .ToArray();
        if (request.AssessmentIds.Count == 0 || assessmentIds.Length == 0)
        {
            assessmentIds = assessmentScope
                .GroupBy(assessment => assessment.FrameworkId)
                .Select(group => group
                    .OrderByDescending(assessment => assessment.SnapshotDate)
                    .ThenByDescending(assessment => assessment.UpdatedAt)
                    .ThenByDescending(assessment => assessment.Id)
                    .First()
                    .Id)
                .ToArray();
        }
        var assessmentOptions = assessmentScope
            .Select(assessment => new DashboardAssessmentOption(
                assessment.Id,
                assessment.FrameworkId,
                assessment.FrameworkName,
                assessment.FrameworkVersion,
                assessment.Label,
                assessment.SnapshotDate))
            .ToArray();
        int? baselineProfileId = null;
        if (request.BaselineProfileId is not null)
        {
            baselineProfileId = await dbContext.BaselineProfiles
                .AsNoTracking()
                .Where(profile =>
                    profile.Id == request.BaselineProfileId &&
                    profile.OrganizationId == request.OrganizationId)
                .Select(profile => (int?)profile.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else if (request.UseDefaultBaseline)
        {
            baselineProfileId = await dbContext.BaselineProfiles
                .AsNoTracking()
                .Where(profile =>
                    profile.OrganizationId == request.OrganizationId &&
                    profile.IsDefault)
                .OrderByDescending(profile => profile.UpdatedAt)
                .ThenByDescending(profile => profile.Id)
                .Select(profile => (int?)profile.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var baselineOptions = await dbContext.BaselineProfiles
            .AsNoTracking()
            .Where(profile => profile.OrganizationId == request.OrganizationId)
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.Name)
            .Select(profile => new DashboardBaselineOption(
                profile.Id,
                profile.Name,
                profile.IsDefault))
            .ToArrayAsync(cancellationToken);

        var assessments = await dbContext.Assessments
            .AsNoTracking()
            .Where(assessment =>
                assessment.OrganizationId == request.OrganizationId &&
                assessmentIds.Contains(assessment.Id))
            .OrderBy(assessment => assessment.Framework.Name)
            .ThenBy(assessment => assessment.Framework.Version)
            .ThenBy(assessment => assessment.Label)
            .Select(assessment => new
            {
                assessment.Id,
                assessment.FrameworkId,
                FrameworkName = assessment.Framework.Name,
                FrameworkVersion = assessment.Framework.Version,
                assessment.Label,
                assessment.UpdatedAt,
                ControlCount = dbContext.Controls.Count(
                    control => control.Domain.FrameworkId == assessment.FrameworkId),
                AssessedCount = assessment.Results.Count(
                    result => result.Status != ComplianceStatus.NotAssessed),
                CompliantCount = assessment.Results.Count(
                    result => result.Status == ComplianceStatus.Compliant),
                GapCount = assessment.Results.Count(
                    result =>
                        result.Status == ComplianceStatus.Partial ||
                        result.Status == ComplianceStatus.NonCompliant)
            })
            .ToArrayAsync(cancellationToken);

        var targetByFrameworkId = baselineProfileId is null
            ? new Dictionary<int, decimal>()
            : await dbContext.BaselineTargets
                .AsNoTracking()
                .Where(target =>
                    target.BaselineProfileId == baselineProfileId &&
                    target.BaselineProfile.OrganizationId == request.OrganizationId &&
                    target.DomainId == null &&
                    target.TargetCompliancePercent != null)
                .ToDictionaryAsync(
                    target => target.FrameworkId,
                    target => target.TargetCompliancePercent!.Value,
                    cancellationToken);

        var overviews = assessments
            .Select(assessment =>
            {
                var compliancePercent =
                    Percent(assessment.CompliantCount, assessment.AssessedCount);
                var hasTarget = targetByFrameworkId.TryGetValue(
                    assessment.FrameworkId,
                    out var targetPercent);
                return new AssessmentOverview(
                    assessment.Id,
                    assessment.FrameworkId,
                    assessment.FrameworkName,
                    assessment.FrameworkVersion,
                    assessment.Label,
                    Percent(assessment.AssessedCount, assessment.ControlCount),
                    compliancePercent,
                    assessment.GapCount,
                    hasTarget ? targetPercent : null,
                    hasTarget ? compliancePercent - targetPercent : null,
                    assessment.UpdatedAt);
            })
            .ToArray();

        var radarResults = await dbContext.AssessmentResults
            .AsNoTracking()
            .Where(result =>
                assessmentIds.Contains(result.AssessmentId) &&
                (result.Status == ComplianceStatus.Compliant ||
                 result.Status == ComplianceStatus.Partial ||
                 result.Status == ComplianceStatus.NonCompliant))
            .Select(result => new
            {
                result.AssessmentId,
                result.Status
            })
            .ToArrayAsync(cancellationToken);
        var radarScoreByAssessmentId = radarResults
            .GroupBy(result => result.AssessmentId)
            .ToDictionary(
                group => group.Key,
                group => group.Average(result => result.Status switch
                {
                    ComplianceStatus.Compliant => 100m,
                    ComplianceStatus.Partial => 50m,
                    _ => 0m
                }));
        var radarAxes = assessments
            .Select(assessment => new RadarAxis(
                assessment.FrameworkId,
                $"{assessment.FrameworkName} {assessment.FrameworkVersion}"))
            .DistinctBy(axis => axis.FrameworkId)
            .ToArray();
        var radarSeries = assessments
            .Select(assessment => new RadarSeries(
                assessment.Id,
                $"{assessment.FrameworkName} — {assessment.Label}",
                radarAxes
                    .Select(axis =>
                        axis.FrameworkId == assessment.FrameworkId &&
                        radarScoreByAssessmentId.TryGetValue(
                            assessment.Id,
                            out var score)
                                ? (decimal?)score
                                : 0m)
                    .ToArray()))
            .ToArray();
        var targetSeries = baselineProfileId is null
            ? null
            : new RadarSeries(
                null,
                "Target",
                radarAxes
                    .Select(axis =>
                        targetByFrameworkId.TryGetValue(
                            axis.FrameworkId,
                            out var target)
                                ? (decimal?)target
                                : null)
                    .ToArray(),
                IsTarget: true);
        var radar = new RadarChart(
            radarAxes,
            [25m, 50m, 75m, 100m],
            radarSeries,
            targetSeries);

        var surveyTemplateOptions = await dbContext.SurveyTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.Name)
            .Select(template => new DashboardSurveyTemplateOption(
                template.Id,
                template.Name,
                template.Cadence))
            .ToArrayAsync(cancellationToken);
        var selectedSurveyTemplateId = request.SurveyTemplateId is not null &&
            surveyTemplateOptions.Any(option => option.Id == request.SurveyTemplateId)
                ? request.SurveyTemplateId
                : null;
        SurveyTracking? surveyTracking = null;
        if (selectedSurveyTemplateId is not null)
        {
            var cadence = await SurveyCadenceService.GetStatusAsync(
                dbContext,
                request.OrganizationId,
                selectedSurveyTemplateId.Value,
                request.CurrentDate?.Date ?? DateTime.UtcNow.Date,
                cancellationToken);
            var submissions = await dbContext.SurveySubmissions
                .AsNoTracking()
                .Where(submission =>
                    submission.OrganizationId == request.OrganizationId &&
                    submission.SurveyTemplateId == selectedSurveyTemplateId)
                .Include(submission => submission.Responses)
                .ThenInclude(response => response.SurveyQuestion)
                .ThenInclude(question => question.Domain)
                .OrderByDescending(submission => submission.SnapshotDate)
                .ThenByDescending(submission => submission.SubmittedAt)
                .ThenByDescending(submission => submission.Id)
                .ToArrayAsync(cancellationToken);
            var scoredSubmissions = submissions
                .Select(submission => new
                {
                    Submission = submission,
                    Score = SurveyScoringService.CalculateProfileScore(
                        submission.Responses)
                })
                .ToArray();
            var history = scoredSubmissions
                .Select((item, index) =>
                {
                    var previousScore = index + 1 < scoredSubmissions.Length
                        ? scoredSubmissions[index + 1].Score
                        : null;
                    var delta = item.Score is not null && previousScore is not null
                        ? item.Score.Value - previousScore.Value
                        : (decimal?)null;
                    return new SurveyHistoryItem(
                        item.Submission.Id,
                        item.Submission.Label,
                        item.Submission.SnapshotDate,
                        item.Score,
                        delta,
                        item.Submission.Notes);
                })
                .ToArray();
            var trend = history
                .Where(item => item.Score is not null)
                .OrderBy(item => item.SnapshotDate)
                .ThenBy(item => item.SubmissionId)
                .Select(item => new SurveyTrendPoint(
                    item.SnapshotDate,
                    item.Score!.Value))
                .ToArray();
            var domainDeltas = Array.Empty<SurveyDomainDelta>();
            if (submissions.Length >= 2)
            {
                var latestDomainScores =
                    SurveyScoringService.CalculateDomainScores(
                        submissions[0].Responses);
                var previousDomainScores =
                    SurveyScoringService.CalculateDomainScores(
                        submissions[1].Responses);
                domainDeltas = submissions[0].Responses
                    .Where(response => response.SurveyQuestion.Domain is not null)
                    .Select(response => response.SurveyQuestion.Domain!)
                    .DistinctBy(domain => domain.Id)
                    .Where(domain =>
                        latestDomainScores.ContainsKey(domain.Id) &&
                        previousDomainScores.ContainsKey(domain.Id))
                    .OrderBy(domain => domain.SortOrder)
                    .ThenBy(domain => domain.Code)
                    .Select(domain => new SurveyDomainDelta(
                        domain.Id,
                        domain.Code,
                        domain.Name,
                        latestDomainScores[domain.Id],
                        previousDomainScores[domain.Id],
                        latestDomainScores[domain.Id] -
                            previousDomainScores[domain.Id]))
                    .ToArray();
            }

            surveyTracking = new SurveyTracking(
                cadence!.TemplateId,
                cadence.Name,
                scoredSubmissions.FirstOrDefault()?.Score,
                history.FirstOrDefault()?.Delta,
                cadence.Status,
                cadence.NextDueDate,
                history,
                trend,
                domainDeltas);
        }

        var gapQuery = dbContext.AssessmentResults
            .AsNoTracking()
            .Where(result =>
                result.Assessment.OrganizationId == request.OrganizationId &&
                assessmentIds.Contains(result.AssessmentId) &&
                (result.Status == ComplianceStatus.Partial ||
                 result.Status == ComplianceStatus.NonCompliant));
        if (request.FrameworkId is not null)
        {
            gapQuery = gapQuery.Where(result =>
                result.Assessment.FrameworkId == request.FrameworkId);
        }

        if (request.DomainId is not null)
        {
            gapQuery = gapQuery.Where(result =>
                result.Control.DomainId == request.DomainId);
        }

        if (request.GapStatus is ComplianceStatus.Partial or
            ComplianceStatus.NonCompliant)
        {
            gapQuery = gapQuery.Where(result => result.Status == request.GapStatus);
        }

        var gaps = await gapQuery
            .Select(result => new DashboardGap(
                result.AssessmentId,
                result.Assessment.FrameworkId,
                result.Assessment.Framework.Name,
                result.Assessment.Framework.Version,
                result.Control.DomainId,
                result.Control.Domain.Code,
                result.Control.Domain.Name,
                result.ControlId,
                result.Control.Code,
                result.Control.Title,
                result.Status,
                result.Score,
                result.Notes))
            .ToArrayAsync(cancellationToken);

        gaps = SortGaps(gaps, request.Sort, request.SortDescending);
        var selectedFrameworkIds = assessmentScope
            .Where(assessment => assessmentIds.Contains(assessment.Id))
            .Select(assessment => assessment.FrameworkId)
            .Distinct()
            .ToArray();
        var frameworkFilters = await dbContext.Frameworks
            .AsNoTracking()
            .Where(framework => selectedFrameworkIds.Contains(framework.Id))
            .OrderBy(framework => framework.Name)
            .ThenBy(framework => framework.Version)
            .Select(framework => new DashboardFrameworkFilter(
                framework.Id,
                framework.Name,
                framework.Version,
                framework.Domains
                    .OrderBy(domain => domain.SortOrder)
                    .ThenBy(domain => domain.Code)
                    .Select(domain => new DashboardDomainFilter(
                        domain.Id,
                        domain.Code,
                        domain.Name))
                    .ToArray()))
            .ToArrayAsync(cancellationToken);
        return new DashboardView(
            assessmentIds,
            baselineProfileId,
            assessmentOptions,
            baselineOptions,
            frameworkFilters,
            overviews,
            gaps,
            radar,
            selectedSurveyTemplateId,
            surveyTemplateOptions,
            surveyTracking);
    }

    private static decimal Percent(int numerator, int denominator) =>
        denominator == 0 ? 0m : numerator * 100m / denominator;

    private static DashboardGap[] SortGaps(
        DashboardGap[] gaps,
        DashboardGapSort sort,
        bool descending)
    {
        Func<DashboardGap, object?> keySelector = sort switch
        {
            DashboardGapSort.Framework => gap => gap.FrameworkName,
            DashboardGapSort.Domain => gap => gap.DomainName,
            DashboardGapSort.Title => gap => gap.Title,
            DashboardGapSort.Status => gap => gap.Status,
            DashboardGapSort.Score => gap => gap.Score,
            _ => gap => gap.ControlCode
        };
        var ordered = descending
            ? gaps.OrderByDescending(keySelector)
            : gaps.OrderBy(keySelector);
        return ordered
            .ThenBy(gap => gap.ControlCode)
            .ThenBy(gap => gap.AssessmentId)
            .ToArray();
    }
}

public sealed record DashboardRequest(
    int OrganizationId,
    IReadOnlyCollection<int> AssessmentIds,
    int? BaselineProfileId = null,
    bool UseDefaultBaseline = true,
    int? FrameworkId = null,
    int? DomainId = null,
    ComplianceStatus? GapStatus = null,
    DashboardGapSort Sort = DashboardGapSort.ControlCode,
    bool SortDescending = false,
    int? SurveyTemplateId = null,
    DateTime? CurrentDate = null);

public sealed record DashboardView(
    int[] SelectedAssessmentIds,
    int? SelectedBaselineProfileId,
    DashboardAssessmentOption[] AssessmentOptions,
    DashboardBaselineOption[] BaselineOptions,
    DashboardFrameworkFilter[] FrameworkFilters,
    AssessmentOverview[] Overviews,
    DashboardGap[] Gaps,
    RadarChart Radar,
    int? SelectedSurveyTemplateId,
    DashboardSurveyTemplateOption[] SurveyTemplateOptions,
    SurveyTracking? SurveyTracking);

public sealed record DashboardAssessmentOption(
    int Id,
    int FrameworkId,
    string FrameworkName,
    string FrameworkVersion,
    string Label,
    DateTime SnapshotDate);

public sealed record DashboardBaselineOption(
    int Id,
    string Name,
    bool IsDefault);

public sealed record DashboardSurveyTemplateOption(
    int Id,
    string Name,
    SurveyCadence Cadence);

public sealed record DashboardFrameworkFilter(
    int Id,
    string Name,
    string Version,
    DashboardDomainFilter[] Domains);

public sealed record DashboardDomainFilter(
    int Id,
    string Code,
    string Name);

public sealed record AssessmentOverview(
    int AssessmentId,
    int FrameworkId,
    string FrameworkName,
    string FrameworkVersion,
    string AssessmentLabel,
    decimal CompletionPercent,
    decimal CompliancePercent,
    int GapCount,
    decimal? TargetCompliancePercent,
    decimal? TargetDelta,
    DateTime UpdatedAt);

public sealed record DashboardGap(
    int AssessmentId,
    int FrameworkId,
    string FrameworkName,
    string FrameworkVersion,
    int DomainId,
    string DomainCode,
    string DomainName,
    int ControlId,
    string ControlCode,
    string Title,
    ComplianceStatus Status,
    decimal? Score,
    string? Notes);

public sealed record RadarChart(
    RadarAxis[] Axes,
    decimal[] GridLevels,
    RadarSeries[] Series,
    RadarSeries? TargetSeries);

public sealed record RadarAxis(int FrameworkId, string Label);

public sealed record RadarSeries(
    int? AssessmentId,
    string Label,
    decimal?[] Values,
    bool IsTarget = false);

public sealed record SurveyTracking(
    int SurveyTemplateId,
    string SurveyTemplateName,
    decimal? LatestScore,
    decimal? LatestDelta,
    SurveyDueStatus CadenceStatus,
    DateTime? NextDueDate,
    SurveyHistoryItem[] History,
    SurveyTrendPoint[] Trend,
    SurveyDomainDelta[] DomainDeltas);

public sealed record SurveyHistoryItem(
    int SubmissionId,
    string Label,
    DateTime SnapshotDate,
    decimal? Score,
    decimal? Delta,
    string? Notes);

public sealed record SurveyTrendPoint(DateTime SnapshotDate, decimal Score);

public sealed record SurveyDomainDelta(
    int DomainId,
    string DomainCode,
    string DomainName,
    decimal LatestScore,
    decimal PreviousScore,
    decimal Delta);

public enum DashboardGapSort
{
    Framework,
    Domain,
    ControlCode,
    Title,
    Status,
    Score
}
