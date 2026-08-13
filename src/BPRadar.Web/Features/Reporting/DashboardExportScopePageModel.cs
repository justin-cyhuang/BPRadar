using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Features.Reporting;

public abstract class DashboardExportScopePageModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? OrganizationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int[] AssessmentIds { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? BaselineProfileId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SurveyTemplateId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FrameworkId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DomainId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ComplianceStatus? GapStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public DashboardGapSort Sort { get; set; } = DashboardGapSort.ControlCode;

    [BindProperty(SupportsGet = true)]
    public bool SortDescending { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool IncludeSurveyDomainDeltas { get; set; }

    protected string CorrelationId =>
        BPRadarTrace.CorrelationId ?? HttpContext.TraceIdentifier;

    protected DashboardRequest CreateDashboardRequest(DateTime currentDate)
    {
        if (OrganizationId is null)
        {
            throw new InvalidOperationException(
                "An organization is required to create a dashboard request.");
        }

        return new DashboardRequest(
            OrganizationId.Value,
            AssessmentIds,
            BaselineProfileId,
            UseDefaultBaseline:
                !Request.Query.ContainsKey(nameof(BaselineProfileId)),
            FrameworkId,
            DomainId,
            GapStatus,
            Sort,
            SortDescending,
            SurveyTemplateId,
            currentDate);
    }

    protected IActionResult ExportScopeNotFound()
    {
        return ExportProblem(new ProblemDetails
        {
            Title = "Dashboard export scope was not found.",
            Detail = "The requested organization has no exportable assessments.",
            Status = StatusCodes.Status404NotFound
        });
    }

    protected IActionResult ExportScopeValidationError()
    {
        return ExportProblem(new ValidationProblemDetails(ModelState)
        {
            Title = "Dashboard export scope is invalid.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private IActionResult ExportProblem(ProblemDetails problem)
    {
        problem.Extensions["correlationId"] = CorrelationId;
        var result = new ObjectResult(problem)
        {
            StatusCode = problem.Status
        };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    protected string TraceScope(DashboardView dashboard)
    {
        var frameworkIds = dashboard.AssessmentOptions
            .Where(option => dashboard.SelectedAssessmentIds.Contains(option.Id))
            .Select(option => option.FrameworkId)
            .Distinct()
            .Order()
            .ToArray();
        return
            $"OrganizationId={Id(OrganizationId)} " +
            $"AssessmentIds={Ids(dashboard.SelectedAssessmentIds)} " +
            $"BaselineProfileId={Id(dashboard.SelectedBaselineProfileId)} " +
            $"FrameworkIds={Ids(frameworkIds)} " +
            $"DomainId={Id(DomainId)} " +
            $"SurveyTemplateId={Id(dashboard.SelectedSurveyTemplateId)}";
    }

    private static string Id(int? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";

    private static string Ids(IEnumerable<int> values) =>
        string.Join(
            ',',
            values.Select(value =>
                value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
