using BPRadar.Web.Features.Dashboard;

namespace BPRadar.Web.Features.Reporting;

public static class DashboardAssessmentScopeFormatter
{
    public static string Format(
        DashboardView dashboard,
        DashboardAssessmentScopeLabelMode mode)
    {
        var selectedOptions = SelectedOptions(dashboard);
        return mode switch
        {
            DashboardAssessmentScopeLabelMode.Assessments => string.Join(
                "; ",
                selectedOptions.Select(option =>
                    $"{option.FrameworkName} {option.FrameworkVersion} - {option.Label}")),
            DashboardAssessmentScopeLabelMode.Frameworks => string.Join(
                ',',
                SelectedFrameworks(selectedOptions)
                    .Select(option => option.DisplayName)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    public static IEnumerable<int> FrameworkIds(DashboardView dashboard) =>
        SelectedFrameworks(SelectedOptions(dashboard))
            .Select(option => option.FrameworkId);

    private static IEnumerable<DashboardAssessmentOption> SelectedOptions(
        DashboardView dashboard) =>
        dashboard.AssessmentOptions.Where(option =>
            dashboard.SelectedAssessmentIds.Contains(option.Id));

    private static IEnumerable<DashboardAssessmentFramework> SelectedFrameworks(
        IEnumerable<DashboardAssessmentOption> selectedOptions) =>
        selectedOptions
            .Select(option => new DashboardAssessmentFramework(
                option.FrameworkId,
                $"{option.FrameworkName} {option.FrameworkVersion}"))
            .Distinct()
            .OrderBy(option => option.FrameworkId);

    private sealed record DashboardAssessmentFramework(
        int FrameworkId,
        string DisplayName);
}

public enum DashboardAssessmentScopeLabelMode
{
    Assessments,
    Frameworks
}
