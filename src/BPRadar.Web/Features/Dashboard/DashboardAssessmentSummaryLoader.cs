using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Dashboard;

internal static class DashboardAssessmentSummaryLoader
{
    public static Task<DashboardAssessmentSummary[]> LoadAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int[] assessmentIds,
        CancellationToken cancellationToken = default) =>
        dbContext.Assessments
            .AsNoTracking()
            .Where(assessment =>
                assessment.OrganizationId == organizationId &&
                assessmentIds.Contains(assessment.Id))
            .OrderBy(assessment => assessment.Framework.Name)
            .ThenBy(assessment => assessment.Framework.Version)
            .ThenBy(assessment => assessment.Label)
            .Select(assessment => new DashboardAssessmentSummary(
                assessment.Id,
                assessment.FrameworkId,
                assessment.Framework.Name,
                assessment.Framework.Version,
                assessment.Label,
                assessment.UpdatedAt,
                dbContext.Controls.Count(
                    control => control.Domain.FrameworkId == assessment.FrameworkId),
                assessment.Results.Count(
                    result => result.Status != ComplianceStatus.NotAssessed),
                assessment.Results.Count(
                    result => result.Status == ComplianceStatus.Compliant),
                assessment.Results.Count(
                    result =>
                        result.Status == ComplianceStatus.Partial ||
                        result.Status == ComplianceStatus.NonCompliant)))
            .ToArrayAsync(cancellationToken);
}

internal sealed record DashboardAssessmentSummary(
    int Id,
    int FrameworkId,
    string FrameworkName,
    string FrameworkVersion,
    string Label,
    DateTime UpdatedAt,
    int ControlCount,
    int AssessedCount,
    int CompliantCount,
    int GapCount);
