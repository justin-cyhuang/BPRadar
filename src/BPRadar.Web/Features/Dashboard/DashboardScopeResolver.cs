using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Dashboard;

internal static class DashboardScopeResolver
{
    public static async Task<DashboardScope> ResolveAsync(
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
        var selectedFrameworkIds = assessmentScope
            .Where(assessment => assessmentIds.Contains(assessment.Id))
            .Select(assessment => assessment.FrameworkId)
            .Distinct()
            .ToArray();
        var baselineProfileId = await ResolveBaselineProfileIdAsync(
            dbContext,
            request,
            cancellationToken);
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

        return new DashboardScope(
            assessmentIds,
            baselineProfileId,
            assessmentOptions,
            baselineOptions,
            selectedFrameworkIds,
            targetByFrameworkId);
    }

    private static async Task<int?> ResolveBaselineProfileIdAsync(
        BPRadarDbContext dbContext,
        DashboardRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BaselineProfileId is not null)
        {
            return await dbContext.BaselineProfiles
                .AsNoTracking()
                .Where(profile =>
                    profile.Id == request.BaselineProfileId &&
                    profile.OrganizationId == request.OrganizationId)
                .Select(profile => (int?)profile.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (!request.UseDefaultBaseline)
        {
            return null;
        }

        return await dbContext.BaselineProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.OrganizationId == request.OrganizationId &&
                profile.IsDefault)
            .OrderByDescending(profile => profile.UpdatedAt)
            .ThenByDescending(profile => profile.Id)
            .Select(profile => (int?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

internal sealed record DashboardScope(
    int[] AssessmentIds,
    int? SelectedBaselineProfileId,
    DashboardAssessmentOption[] AssessmentOptions,
    DashboardBaselineOption[] BaselineOptions,
    int[] SelectedFrameworkIds,
    IReadOnlyDictionary<int, decimal> TargetByFrameworkId);
