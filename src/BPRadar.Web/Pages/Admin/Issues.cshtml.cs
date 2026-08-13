using BPRadar.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages.Admin;

public sealed class IssuesModel(BPRadarDbContext dbContext) : PageModel
{
    public IReadOnlyList<OrganizationOption> Organizations { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Organizations = await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(organization => organization.Name)
            .Select(organization => new OrganizationOption(
                organization.Id,
                organization.Name))
            .ToArrayAsync(cancellationToken);
    }
}

public sealed record OrganizationOption(int Id, string Name);
