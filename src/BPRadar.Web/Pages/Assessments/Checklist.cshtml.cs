using BPRadar.Web.Data;
using BPRadar.Web.Features.ManualEntry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Assessments;

public sealed class ChecklistModel(BPRadarDbContext dbContext) : PageModel
{
    public AssessmentChecklist Checklist { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        var checklist = await ManualEntryService.GetChecklistAsync(
            dbContext,
            assessmentId,
            cancellationToken);
        if (checklist is null)
        {
            return NotFound();
        }

        Checklist = checklist;
        return Page();
    }
}
