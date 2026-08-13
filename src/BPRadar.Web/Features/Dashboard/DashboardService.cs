using BPRadar.Web.Data;
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
            gaps);
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
    bool SortDescending = false);

public sealed record DashboardView(
    int[] SelectedAssessmentIds,
    int? SelectedBaselineProfileId,
    DashboardAssessmentOption[] AssessmentOptions,
    DashboardBaselineOption[] BaselineOptions,
    DashboardFrameworkFilter[] FrameworkFilters,
    AssessmentOverview[] Overviews,
    DashboardGap[] Gaps);

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

public enum DashboardGapSort
{
    Framework,
    Domain,
    ControlCode,
    Title,
    Status,
    Score
}
