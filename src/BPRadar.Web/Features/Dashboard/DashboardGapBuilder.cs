using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Dashboard;

internal static class DashboardGapBuilder
{
    public static async Task<DashboardGapResult> BuildAsync(
        BPRadarDbContext dbContext,
        DashboardRequest request,
        int[] assessmentIds,
        int[] selectedFrameworkIds,
        CancellationToken cancellationToken = default)
    {
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

        return new DashboardGapResult(gaps, frameworkFilters);
    }

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

internal sealed record DashboardGapResult(
    DashboardGap[] Gaps,
    DashboardFrameworkFilter[] FrameworkFilters);
