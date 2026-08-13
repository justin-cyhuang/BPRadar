using System.Diagnostics;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages;

public sealed class DashboardReportModel(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider) : PageModel
{
    public const int GapLimit = 50;

    [BindProperty(SupportsGet = true)]
    public int OrganizationId { get; set; }

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

    public DashboardView Dashboard { get; private set; } = null!;

    public string OrganizationName { get; private set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public DashboardGap[] PrintedGaps => Dashboard.Gaps.Take(GapLimit).ToArray();

    public int OmittedGapCount => Math.Max(0, Dashboard.Gaps.Length - GapLimit);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Reporting",
            "PrintReportStarted",
            $"organizationId={OrganizationId}");
        OrganizationName = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization =>
                organization.Id == OrganizationId &&
                dbContext.Assessments.Any(
                    assessment => assessment.OrganizationId == organization.Id))
            .Select(organization => organization.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        if (OrganizationName.Length == 0)
        {
            return NotFound();
        }

        var baselineSelectionSpecified =
            Request.Query.ContainsKey(nameof(BaselineProfileId));
        Dashboard = await DashboardService.GetAsync(
            dbContext,
            new DashboardRequest(
                OrganizationId,
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
        GeneratedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        CorrelationId = BPRadarTrace.CorrelationId ?? HttpContext.TraceIdentifier;
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Reporting",
            "PrintReportCompleted",
            $"organizationId={OrganizationId} assessments={Dashboard.SelectedAssessmentIds.Length} " +
            $"gaps={Dashboard.Gaps.Length} printedGaps={PrintedGaps.Length}",
            timer.ElapsedMilliseconds);
        return Page();
    }
}
