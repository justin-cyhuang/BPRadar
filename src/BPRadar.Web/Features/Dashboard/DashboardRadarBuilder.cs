using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Dashboard;

internal static class DashboardRadarBuilder
{
    public static async Task<RadarChart> BuildAsync(
        BPRadarDbContext dbContext,
        int[] assessmentIds,
        IReadOnlyCollection<DashboardAssessmentSummary> assessments,
        int? baselineProfileId,
        IReadOnlyDictionary<int, decimal> targetByFrameworkId,
        CancellationToken cancellationToken = default)
    {
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

        return new RadarChart(
            radarAxes,
            [25m, 50m, 75m, 100m],
            radarSeries,
            targetSeries);
    }
}
