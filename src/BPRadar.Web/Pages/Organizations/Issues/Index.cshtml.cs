using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Issues;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BPRadar.Web.Pages.Organizations.Issues;

public sealed class IndexModel(BPRadarDbContext dbContext) : PageModel
{
    public int OrganizationId { get; private set; }
    public string OrganizationName { get; private set; } = string.Empty;
    public IReadOnlyList<IssueDetail> Issues { get; private set; } = [];

    [TempData]
    public string? ConfirmationMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        var organizationName = await OrganizationQueries.GetNameAsync(
            dbContext,
            organizationId,
            cancellationToken);
        if (organizationName is null)
        {
            return NotFound();
        }

        OrganizationId = organizationId;
        OrganizationName = organizationName;
        var issues = await dbContext.Issues
            .AsNoTracking()
            .Where(issue => issue.OrganizationId == organizationId)
            .Include(issue => issue.ViolationMatches)
            .ThenInclude(match => match.Control)
            .OrderByDescending(issue => issue.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var details = new List<IssueDetail>(issues.Length);
        foreach (var issue in issues)
        {
            details.Add(await IssueEndpoints.ToDetailAsync(
                dbContext,
                issue,
                cancellationToken));
        }

        Issues = details;
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int organizationId,
        int violationMatchId,
        string reviewStatus,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ViolationMatchReviewStatus>(
                reviewStatus,
                ignoreCase: true,
                out var parsedStatus) ||
            parsedStatus is not (
                ViolationMatchReviewStatus.Confirmed or
                ViolationMatchReviewStatus.Dismissed))
        {
            return BadRequest();
        }

        var match = await dbContext.ViolationMatches
            .Include(item => item.Issue)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == violationMatchId &&
                    item.Issue.OrganizationId == organizationId,
                cancellationToken);
        if (match is null)
        {
            return NotFound();
        }

        match.ReviewStatus = parsedStatus;
        await dbContext.SaveChangesAsync(cancellationToken);
        BPRadarTrace.Write(
            TraceEventType.Information,
            "Issues",
            "ViolationMatchReviewed",
            $"ViolationMatchId={match.Id} IssueId={match.IssueId} " +
            $"ControlId={match.ControlId} ReviewStatus={parsedStatus}");
        ConfirmationMessage = parsedStatus == ViolationMatchReviewStatus.Confirmed
            ? "Violation Match confirmed."
            : "Violation Match dismissed.";
        return RedirectToPage(new { organizationId });
    }
}
