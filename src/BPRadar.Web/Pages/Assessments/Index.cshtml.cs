using BPRadar.Web.Data;
using BPRadar.Web.Features.Assessments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Assessments;

public sealed class IndexModel(BPRadarDbContext dbContext) : PageModel
{
    public IReadOnlyList<AssessmentSummary> Assessments { get; private set; } = [];

    [TempData]
    public string? ConfirmationMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Assessments = await AssessmentService.ListAsync(
            dbContext,
            cancellationToken);
    }
}
