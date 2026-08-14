using System.Globalization;
using System.Text;
using BPRadar.Web.Data;
using BPRadar.Web.Features.Dashboard;

namespace BPRadar.Web.Features.Reporting;

public static class DashboardCsvExporter
{
    public static byte[] Export(
        DashboardView dashboard,
        string organizationName,
        DateTime exportedAtUtc,
        string correlationId,
        bool includeSurveyDomainDeltas)
    {
        var csv = new StringBuilder();
        AddSection(csv, "Report Metadata");
        AddRow(csv, "Organization", organizationName);
        AddRow(csv, "Selected Assessments", string.Join(
            "; ",
            dashboard.AssessmentOptions
                .Where(option => dashboard.SelectedAssessmentIds.Contains(option.Id))
                .Select(option =>
                    $"{option.FrameworkName} {option.FrameworkVersion} - {option.Label}")));
        AddRow(
            csv,
            "Survey Template",
            dashboard.SurveyTracking?.SurveyTemplateName ?? "Not selected");
        AddRow(csv, "Survey Evidence State", SurveyEvidenceState(dashboard));
        AddRow(csv, "Exported UTC", exportedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AddRow(csv, "Correlation ID", correlationId);

        AddSection(csv, "Framework Summary");
        AddRow(
            csv,
            "Framework",
            "Version",
            "Assessment",
            "Completion %",
            "Compliance %",
            "Target %",
            "Delta %",
            "Gap Count");
        foreach (var overview in dashboard.Overviews)
        {
            AddRow(
                csv,
                overview.FrameworkName,
                overview.FrameworkVersion,
                overview.AssessmentLabel,
                Number(overview.CompletionPercent),
                Number(overview.CompliancePercent),
                Number(overview.TargetCompliancePercent),
                Number(overview.TargetDelta),
                overview.GapCount.ToString(CultureInfo.InvariantCulture));
        }

        AddSection(csv, "Gap List");
        AddRow(
            csv,
            "Framework",
            "Domain",
            "Control Code",
            "Title",
            "Status",
            "Score",
            "Notes");
        foreach (var gap in dashboard.Gaps)
        {
            AddRow(
                csv,
                gap.FrameworkName,
                $"{gap.DomainCode} - {gap.DomainName}",
                gap.ControlCode,
                gap.Title,
                gap.Status.ToDisplayText(),
                Number(gap.Score),
                gap.Notes);
        }

        AddSection(csv, "Survey Submission History");
        AddRow(
            csv,
            "Snapshot Date",
            "Submission",
            "Profile Score",
            "Delta",
            "Key Notes");
        foreach (var submission in dashboard.SurveyTracking?.History ?? [])
        {
            AddRow(
                csv,
                submission.SnapshotDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                submission.Label,
                Number(submission.Score),
                Number(submission.Delta),
                submission.Notes);
        }

        if (includeSurveyDomainDeltas)
        {
            AddSection(csv, "Survey Domain Deltas");
            AddRow(csv, "Domain", "Previous Score", "Latest Score", "Delta");
            foreach (var domain in dashboard.SurveyTracking?.DomainDeltas ?? [])
            {
                AddRow(
                    csv,
                    $"{domain.DomainCode} - {domain.DomainName}",
                    Number(domain.PreviousScore),
                    Number(domain.LatestScore),
                    Number(domain.Delta));
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetBytes(csv.ToString());
    }

    private static void AddSection(StringBuilder csv, string name)
    {
        if (csv.Length > 0)
        {
            csv.AppendLine();
        }

        AddRow(csv, name);
    }

    private static void AddRow(StringBuilder csv, params string?[] fields) =>
        csv.AppendLine(string.Join(',', fields.Select(Escape)));

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 &&
            value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' &&
            !decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out _))
        {
            value = $"'{value}";
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Number(decimal? value) =>
        value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string SurveyEvidenceState(DashboardView dashboard)
    {
        var tracking = dashboard.SurveyTracking;
        if (tracking is null)
        {
            return "No Survey Template selected";
        }

        if (tracking.History.Length == 0)
        {
            return "Selected Survey Template has no submissions";
        }

        return tracking.Trend.Length == 0
            ? "Selected Survey Template has submissions but no scored history"
            : "Scored submission history available";
    }
}
