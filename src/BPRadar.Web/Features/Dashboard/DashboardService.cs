using BPRadar.Web.Data;

namespace BPRadar.Web.Features.Dashboard;

public static class DashboardService
{
    public static async Task<DashboardView> GetAsync(
        BPRadarDbContext dbContext,
        DashboardRequest request,
        CancellationToken cancellationToken = default)
    {
        var surveySubmissionFrom = request.SurveySubmissionFrom?.Date;
        var surveySubmissionTo = request.SurveySubmissionTo?.Date;
        if (surveySubmissionFrom > surveySubmissionTo)
        {
            throw new ArgumentException(
                "Survey submissions from date must be on or before the to date.",
                nameof(request));
        }

        var scope = await DashboardScopeResolver.ResolveAsync(
            dbContext,
            request,
            cancellationToken);
        var assessments = await DashboardAssessmentSummaryLoader.LoadAsync(
            dbContext,
            request.OrganizationId,
            scope.AssessmentIds,
            cancellationToken);
        var overviews = DashboardOverviewBuilder.Build(
            assessments,
            scope.TargetByFrameworkId);
        var radar = await DashboardRadarBuilder.BuildAsync(
            dbContext,
            scope.AssessmentIds,
            assessments,
            scope.SelectedBaselineProfileId,
            scope.TargetByFrameworkId,
            cancellationToken);
        var survey = await DashboardSurveyTrackingBuilder.BuildAsync(
            dbContext,
            request,
            surveySubmissionFrom,
            surveySubmissionTo,
            cancellationToken);
        var gaps = await DashboardGapBuilder.BuildAsync(
            dbContext,
            request,
            scope.AssessmentIds,
            scope.SelectedFrameworkIds,
            cancellationToken);

        return new DashboardView(
            scope.AssessmentIds,
            scope.SelectedBaselineProfileId,
            scope.AssessmentOptions,
            scope.BaselineOptions,
            gaps.FrameworkFilters,
            overviews,
            gaps.Gaps,
            radar,
            survey.SelectedSurveyTemplateId,
            surveySubmissionFrom,
            surveySubmissionTo,
            survey.TemplateOptions,
            survey.Tracking);
    }
}
