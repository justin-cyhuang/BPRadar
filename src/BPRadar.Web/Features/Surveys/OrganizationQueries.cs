using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Surveys;

public static class OrganizationQueries
{
    public static Task<string?> GetNameAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => organization.Name)
            .SingleOrDefaultAsync(cancellationToken);

    public static Task<OrganizationSummary[]> ListAsync(
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        dbContext.Organizations
            .AsNoTracking()
            .OrderBy(organization => organization.Name)
            .Select(organization => new OrganizationSummary(
                organization.Id,
                organization.Name))
            .ToArrayAsync(cancellationToken);
}

public sealed record OrganizationSummary(int Id, string Name);
