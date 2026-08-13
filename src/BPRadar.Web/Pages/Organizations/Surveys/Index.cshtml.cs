using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Organizations.Surveys;

public sealed class IndexModel(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider) : PageModel
{
    public int OrganizationId { get; private set; }
    public string OrganizationName { get; private set; } = string.Empty;
    public IReadOnlyList<DueSurveyTemplate> DueTemplates { get; private set; } = [];

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
        DueTemplates = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organizationId,
            timeProvider.GetUtcNow().UtcDateTime.Date,
            cancellationToken);
        return Page();
    }
}
