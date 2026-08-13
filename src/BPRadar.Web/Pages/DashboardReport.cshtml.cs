using System.Diagnostics;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Dashboard;
using BPRadar.Web.Features.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages;

public sealed class DashboardReportModel(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider) : DashboardExportScopePageModel
{
    public const int GapLimit = 50;

    public DashboardView Dashboard { get; private set; } = null!;

    public string OrganizationName { get; private set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; private set; }

    public string ReportCorrelationId { get; private set; } = string.Empty;

    public DashboardGap[] PrintedGaps => Dashboard.Gaps.Take(GapLimit).ToArray();

    public int OmittedGapCount => Math.Max(0, Dashboard.Gaps.Length - GapLimit);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Reporting",
            "PrintReportStarted",
            $"OrganizationId={OrganizationId?.ToString() ?? "none"}");
        if (!ModelState.IsValid)
        {
            return ExportScopeValidationError();
        }

        if (OrganizationId is null)
        {
            return ExportScopeNotFound();
        }

        OrganizationName = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization =>
                organization.Id == OrganizationId.Value &&
                dbContext.Assessments.Any(
                    assessment => assessment.OrganizationId == organization.Id))
            .Select(organization => organization.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        if (OrganizationName.Length == 0)
        {
            return ExportScopeNotFound();
        }

        Dashboard = await DashboardService.GetAsync(
            dbContext,
            CreateDashboardRequest(timeProvider.GetUtcNow().UtcDateTime.Date),
            cancellationToken);
        GeneratedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        ReportCorrelationId = CorrelationId;
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Reporting",
            "PrintReportCompleted",
            $"{TraceScope(Dashboard)} " +
            $"assessments={Dashboard.SelectedAssessmentIds.Length} " +
            $"gaps={Dashboard.Gaps.Length} printedGaps={PrintedGaps.Length}",
            timer.ElapsedMilliseconds);
        return Page();
    }
}
