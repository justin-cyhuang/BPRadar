using BPRadar.Web.Data;
using BPRadar.Web.Features.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages;

public sealed class DashboardModel(BPRadarDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? OrganizationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int[] AssessmentIds { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? BaselineProfileId { get; set; }

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

    public DashboardOrganizationOption[] Organizations { get; private set; } = [];

    public DashboardView? Dashboard { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
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
                SortDescending),
            cancellationToken);
        AssessmentIds = Dashboard.SelectedAssessmentIds;
        BaselineProfileId = Dashboard.SelectedBaselineProfileId;
    }
}

public sealed record DashboardOrganizationOption(int Id, string Name);
