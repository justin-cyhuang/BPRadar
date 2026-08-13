using System.ComponentModel.DataAnnotations;
using BPRadar.Web.Data;
using BPRadar.Web.Features.Assessments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages.Assessments;

public sealed class CreateModel(BPRadarDbContext dbContext) : PageModel
{
    [BindProperty]
    public AssessmentInput Input { get; set; } = new();

    public IReadOnlyList<OrganizationOption> Organizations { get; private set; } = [];
    public IReadOnlyList<FrameworkOption> Frameworks { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Input.SnapshotDate = DateTime.UtcNow.Date;
        await LoadOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await AssessmentService.CreateAsync(
                dbContext,
                new CreateAssessmentRequest(
                    Input.OrganizationId,
                    Input.NewOrganizationName,
                    Input.FrameworkId,
                    Input.Label,
                    Input.SnapshotDate),
                cancellationToken);
            if (result.Errors is null)
            {
                TempData[nameof(IndexModel.ConfirmationMessage)] =
                    $"Assessment \"{result.Assessment!.Label}\" was created.";
                return RedirectToPage("/Assessments/Index");
            }

            foreach (var error in result.Errors)
            {
                foreach (var message in error.Value)
                {
                    ModelState.AddModelError(error.Key, message);
                }
            }
        }

        await LoadOptionsAsync(cancellationToken);
        return Page();
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        Organizations = await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(organization => organization.Name)
            .Select(organization => new OrganizationOption(
                organization.Id,
                organization.Name))
            .ToArrayAsync(cancellationToken);
        Frameworks = await dbContext.Frameworks
            .AsNoTracking()
            .OrderBy(framework => framework.Name)
            .ThenBy(framework => framework.Version)
            .Select(framework => new FrameworkOption(
                framework.Id,
                framework.Name,
                framework.Version))
            .ToArrayAsync(cancellationToken);
    }
}

public sealed class AssessmentInput
{
    public int? OrganizationId { get; set; }

    [StringLength(200)]
    public string? NewOrganizationName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a framework.")]
    public int FrameworkId { get; set; }

    [Required]
    [StringLength(200)]
    public string Label { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime SnapshotDate { get; set; }
}

public sealed record OrganizationOption(int Id, string Name);

public sealed record FrameworkOption(int Id, string Name, string Version);
