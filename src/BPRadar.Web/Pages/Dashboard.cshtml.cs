using System.Diagnostics;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Dashboard;
using BPRadar.Web.Features.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages;

public sealed class DashboardModel(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider) : DashboardExportScopePageModel
{
    public DashboardOrganizationOption[] Organizations { get; private set; } = [];

    public DashboardView? Dashboard { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(
            requireRequestedOrganization: false,
            cancellationToken);
    }

    public async Task<IActionResult> OnGetCsvAsync(
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Reporting",
            "CsvExportStarted",
            $"OrganizationId={OrganizationId?.ToString() ?? "default"}");
        if (!ModelState.IsValid)
        {
            return ExportScopeValidationError();
        }

        var scopeFound = await LoadDashboardAsync(
            requireRequestedOrganization: true,
            cancellationToken);
        if (!scopeFound || Dashboard is null || OrganizationId is null)
        {
            return ExportScopeNotFound();
        }

        var organizationName = Organizations
            .Single(organization => organization.Id == OrganizationId)
            .Name;
        var exportedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var content = DashboardCsvExporter.Export(
            Dashboard,
            organizationName,
            exportedAtUtc,
            CorrelationId,
            IncludeSurveyDomainDeltas);
        var fileName =
            $"bpradar-audit-{exportedAtUtc:yyyyMMdd-HHmmss}.csv";
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Reporting",
            "CsvExportCompleted",
            $"{TraceScope(Dashboard, organizationName)} " +
            $"assessments={Dashboard.SelectedAssessmentIds.Length} " +
            $"gaps={Dashboard.Gaps.Length} bytes={content.Length}",
            timer.ElapsedMilliseconds);
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    private async Task<bool> LoadDashboardAsync(
        bool requireRequestedOrganization,
        CancellationToken cancellationToken)
    {
        var requestedOrganizationId = OrganizationId;
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
            return false;
        }

        if (requireRequestedOrganization &&
            (requestedOrganizationId is null ||
             Organizations.All(
                 organization => organization.Id != requestedOrganizationId)))
        {
            return false;
        }

        if (OrganizationId is null ||
            Organizations.All(organization => organization.Id != OrganizationId))
        {
            OrganizationId = Organizations[0].Id;
        }

        Dashboard = await DashboardService.GetAsync(
            dbContext,
            CreateDashboardRequest(timeProvider.GetUtcNow().UtcDateTime.Date),
            cancellationToken);
        AssessmentIds = Dashboard.SelectedAssessmentIds;
        BaselineProfileId = Dashboard.SelectedBaselineProfileId;
        SurveyTemplateId = Dashboard.SelectedSurveyTemplateId;
        return true;
    }
}

public sealed record DashboardOrganizationOption(int Id, string Name);
