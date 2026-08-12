using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Admin;

public sealed class SurveyTemplatesModel(BPRadarDbContext dbContext) : PageModel
{
    public IReadOnlyList<SurveyTemplateSummary> Templates { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Templates = await SurveyTemplateQueries.ListAsync(
            dbContext,
            cancellationToken);
    }
}
