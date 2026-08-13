using System.Diagnostics;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Dashboard;
using BPRadar.Web.Features.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages;

public sealed class DashboardModel(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider) : PageModel
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

    public DashboardOrganizationOption[] Organizations { get; private set; } = [];

    public DashboardView? Dashboard { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetCsvAsync(
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Reporting",
            "CsvExportStarted",
            $"organizationId={OrganizationId?.ToString() ?? "default"}");
        await LoadDashboardAsync(cancellationToken);
        if (Dashboard is null || OrganizationId is null)
        {
            return NotFound();
        }

        var organizationName = Organizations
            .Single(organization => organization.Id == OrganizationId)
            .Name;
        var exportedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var content = DashboardCsvExporter.Export(
            Dashboard,
            organizationName,
            exportedAtUtc,
            BPRadarTrace.CorrelationId ?? HttpContext.TraceIdentifier,
            IncludeSurveyDomainDeltas);
        var fileName =
            $"bpradar-audit-{exportedAtUtc:yyyyMMdd-HHmmss}.csv";
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Reporting",
            "CsvExportCompleted",
            $"organizationId={OrganizationId} assessments={Dashboard.SelectedAssessmentIds.Length} " +
            $"gaps={Dashboard.Gaps.Length} bytes={content.Length}",
            timer.ElapsedMilliseconds);
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        Organizations = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => dbContext.Assessments.Any(
                assessment => assessment.OrganizationId == organization.Id))
            .OrderBy(organization => organization.Name)
            .Select(organization => new DashboardOrganizationOption(
                organization.Id,
                organization.Name))
            .ToArrayAsync(cancellationToken);
        if (Organizations.Length == 0)
        {
            return;
        }

        if (OrganizationId is null ||
            Organizations.All(organization => organization.Id != OrganizationId))
        {
            OrganizationId = Organizations[0].Id;
        }

        var baselineSelectionSpecified =
            Request.Query.ContainsKey(nameof(BaselineProfileId));
        Dashboard = await DashboardService.GetAsync(
            dbContext,
            new DashboardRequest(
                OrganizationId.Value,
                AssessmentIds,
                BaselineProfileId,
                UseDefaultBaseline: !baselineSelectionSpecified,
                FrameworkId,
                DomainId,
                GapStatus,
                Sort,
                SortDescending,
                SurveyTemplateId,
                timeProvider.GetUtcNow().UtcDateTime.Date),
            cancellationToken);
        AssessmentIds = Dashboard.SelectedAssessmentIds;
        BaselineProfileId = Dashboard.SelectedBaselineProfileId;
        SurveyTemplateId = Dashboard.SelectedSurveyTemplateId;
    }
}

public sealed record DashboardOrganizationOption(int Id, string Name);
