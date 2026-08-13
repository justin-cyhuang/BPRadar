using BPRadar.Web.Data;
using BPRadar.Web.Features.Baselines;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages.Organizations.Baselines;

public sealed class IndexModel(BPRadarDbContext dbContext) : PageModel
{
    public int OrganizationId { get; private set; }
    public string OrganizationName { get; private set; } = string.Empty;
    public IReadOnlyList<BaselineProfileView> Profiles { get; private set; } = [];
    public IReadOnlyList<FrameworkOption> Frameworks { get; private set; } = [];

    [TempData]
    public string? ConfirmationMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        if (!await LoadAsync(organizationId, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCreateProfileAsync(
        int organizationId,
        string name,
        string? description,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var result = await BaselineService.CreateProfileAsync(
            dbContext,
            organizationId,
            name,
            description,
            isDefault,
            cancellationToken);
        return await CompleteAsync(
            result,
            organizationId,
            "Baseline profile created.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync(
        int organizationId,
        int profileId,
        string name,
        string? description,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var result = await BaselineService.UpdateProfileAsync(
            dbContext,
            organizationId,
            profileId,
            name,
            description,
            isDefault,
            cancellationToken);
        return await CompleteAsync(
            result,
            organizationId,
            "Baseline profile updated.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteProfileAsync(
        int organizationId,
        int profileId,
        CancellationToken cancellationToken)
    {
        var result = await BaselineService.DeleteProfileAsync(
            dbContext,
            organizationId,
            profileId,
            cancellationToken);
        return await CompleteAsync(
            result,
            organizationId,
            "Baseline profile deleted.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateTargetAsync(
        int organizationId,
        int profileId,
        int frameworkId,
        int? domainId,
        decimal? targetCompliancePercent,
        decimal? targetScore,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReloadInvalidPostAsync(
                organizationId,
                cancellationToken);
        }

        var result = await BaselineService.CreateTargetAsync(
            dbContext,
            organizationId,
            profileId,
            frameworkId,
            domainId,
            targetCompliancePercent,
            targetScore,
            notes,
            cancellationToken);
        return await CompleteAsync(
            result,
            organizationId,
            "Baseline target created.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateTargetAsync(
        int organizationId,
        int profileId,
        int targetId,
        int frameworkId,
        int? domainId,
        decimal? targetCompliancePercent,
        decimal? targetScore,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReloadInvalidPostAsync(
                organizationId,
                cancellationToken);
        }

        var result = await BaselineService.UpdateTargetAsync(
            dbContext,
            organizationId,
            profileId,
            targetId,
            frameworkId,
            domainId,
            targetCompliancePercent,
            targetScore,
            notes,
            cancellationToken);
        return await CompleteAsync(
            result,
            organizationId,
            "Baseline target updated.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteTargetAsync(
        int organizationId,
        int profileId,
        int targetId,
        CancellationToken cancellationToken)
    {
        var result = await BaselineService.DeleteTargetAsync(
            dbContext,
            organizationId,
            profileId,
            targetId,
            cancellationToken);
        return await CompleteAsync(
            result,
            organizationId,
            "Baseline target deleted.",
            cancellationToken);
    }

    private async Task<IActionResult> CompleteAsync(
        BaselineOperationResult result,
        int organizationId,
        string confirmationMessage,
        CancellationToken cancellationToken)
    {
        if (result.Errors is null)
        {
            ConfirmationMessage = confirmationMessage;
            return RedirectToPage(new { organizationId });
        }

        foreach (var error in result.Errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }

        if (!await LoadAsync(organizationId, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    private async Task<IActionResult> ReloadInvalidPostAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        if (!await LoadAsync(organizationId, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    private async Task<bool> LoadAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .Where(item => item.Id == organizationId)
            .Select(item => new { item.Id, item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (organization is null)
        {
            return false;
        }

        OrganizationId = organization.Id;
        OrganizationName = organization.Name;
        Profiles = await dbContext.BaselineProfiles
            .AsNoTracking()
            .Where(profile => profile.OrganizationId == organizationId)
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.Name)
            .Select(profile => new BaselineProfileView(
                profile.Id,
                profile.Name,
                profile.Description,
                profile.IsDefault,
                profile.Targets
                    .OrderBy(target => target.Framework.Name)
                    .ThenBy(target => target.Domain == null ? -1 : target.Domain.SortOrder)
                    .Select(target => new BaselineTargetView(
                        target.Id,
                        target.FrameworkId,
                        target.Framework.Name,
                        target.Framework.Version,
                        target.DomainId,
                        target.Domain == null
                            ? null
                            : target.Domain.Code + " - " + target.Domain.Name,
                        target.TargetCompliancePercent,
                        target.TargetScore,
                        target.Notes))
                    .ToArray()))
            .ToArrayAsync(cancellationToken);
        Frameworks = await dbContext.Frameworks
            .AsNoTracking()
            .OrderBy(framework => framework.Name)
            .ThenBy(framework => framework.Version)
            .Select(framework => new FrameworkOption(
                framework.Id,
                framework.Name,
                framework.Version,
                framework.Domains
                    .OrderBy(domain => domain.SortOrder)
                    .ThenBy(domain => domain.Code)
                    .Select(domain => new DomainOption(
                        domain.Id,
                        domain.Code,
                        domain.Name))
                    .ToArray()))
            .ToArrayAsync(cancellationToken);
        return true;
    }
}

public sealed record BaselineProfileView(
    int Id,
    string Name,
    string? Description,
    bool IsDefault,
    BaselineTargetView[] Targets);

public sealed record BaselineTargetView(
    int Id,
    int FrameworkId,
    string FrameworkName,
    string FrameworkVersion,
    int? DomainId,
    string? DomainName,
    decimal? TargetCompliancePercent,
    decimal? TargetScore,
    string? Notes);

public sealed record FrameworkOption(
    int Id,
    string Name,
    string Version,
    DomainOption[] Domains);

public sealed record DomainOption(int Id, string Code, string Name);
