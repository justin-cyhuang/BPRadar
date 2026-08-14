using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Organizations.Surveys;

public sealed class ReviewModel(BPRadarDbContext dbContext) : PageModel
{
    public SurveySubmissionReview Submission { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        int organizationId,
        int submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await SurveySubmissionService.GetReviewAsync(
            dbContext,
            organizationId,
            submissionId,
            cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        Submission = submission;
        return Page();
    }

    public static string ResponseLevelLabel(SurveyResponseLevel responseLevel) =>
        responseLevel switch
        {
            SurveyResponseLevel.VeryLow => "Very low",
            SurveyResponseLevel.Low => "Low",
            SurveyResponseLevel.Medium => "Medium",
            SurveyResponseLevel.High => "High",
            SurveyResponseLevel.VeryHigh => "Very high",
            SurveyResponseLevel.NotApplicable => "Not applicable",
            _ => responseLevel.ToString()
        };
}
