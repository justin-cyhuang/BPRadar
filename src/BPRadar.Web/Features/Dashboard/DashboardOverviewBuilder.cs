namespace BPRadar.Web.Features.Dashboard;

internal static class DashboardOverviewBuilder
{
    public static AssessmentOverview[] Build(
        IReadOnlyCollection<DashboardAssessmentSummary> assessments,
        IReadOnlyDictionary<int, decimal> targetByFrameworkId) =>
        assessments
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

    private static decimal Percent(int numerator, int denominator) =>
        denominator == 0 ? 0m : numerator * 100m / denominator;
}
