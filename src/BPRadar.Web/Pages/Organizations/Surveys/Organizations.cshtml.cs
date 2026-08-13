using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Organizations.Surveys;

public sealed class OrganizationsModel(BPRadarDbContext dbContext) : PageModel
{
    public IReadOnlyList<OrganizationSummary> Organizations { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Organizations = await OrganizationQueries.ListAsync(
            dbContext,
            cancellationToken);
    }
}
