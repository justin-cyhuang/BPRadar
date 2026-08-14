using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;

namespace BPRadar.Web.Features.Dashboard;

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
    DateTime? CurrentDate = null,
    DateTime? SurveySubmissionFrom = null,
    DateTime? SurveySubmissionTo = null);

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
    DateTime? SelectedSurveySubmissionFrom,
    DateTime? SelectedSurveySubmissionTo,
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
