using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BPRadar.Web.Pages.Organizations.Surveys;

public sealed class CompleteModel(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider) : PageModel
{
    public int OrganizationId { get; private set; }
    public string OrganizationName { get; private set; } = string.Empty;
    public SurveyTemplateDetail Template { get; private set; } = null!;

    public static IReadOnlyList<SurveyResponseOption> ResponseLevels { get; } =
    [
        new(SurveyResponseLevel.VeryLow, "Very low"),
        new(SurveyResponseLevel.Low, "Low"),
        new(SurveyResponseLevel.Medium, "Medium"),
        new(SurveyResponseLevel.High, "High"),
        new(SurveyResponseLevel.VeryHigh, "Very high"),
        new(SurveyResponseLevel.NotApplicable, "Not applicable")
    ];

    [BindProperty]
    public SurveyFormInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
        int organizationId,
        int templateId,
        CancellationToken cancellationToken)
    {
        if (!await LoadPageAsync(organizationId, templateId, cancellationToken))
        {
            return NotFound();
        }

        Input.Answers = Template.Questions
            .Select(question => new SurveyAnswerInput
            {
                SurveyQuestionId = question.Id
            })
            .ToList();
        var snapshotDate = timeProvider.GetUtcNow().UtcDateTime.Date;
        Input.Label = $"{Template.Name} - {snapshotDate:yyyy-MM-dd}";
        Input.SnapshotDate = snapshotDate;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        int organizationId,
        int templateId,
        CancellationToken cancellationToken)
    {
        if (!await LoadPageAsync(organizationId, templateId, cancellationToken))
        {
            return NotFound();
        }

        var answers = Input.Answers
            .Where(answer => answer.ResponseLevel is not null)
            .Select(answer => new SurveyAnswerRequest(
                answer.SurveyQuestionId,
                answer.ResponseLevel!.Value.ToString(),
                null,
                null))
            .ToArray();
        var result = await SurveySubmissionService.CreateAsync(
            dbContext,
            organizationId,
            new CreateSurveySubmissionRequest(
                templateId,
                Input.Label ?? string.Empty,
                Input.SnapshotDate ?? default,
                Input.Notes,
                answers),
            cancellationToken);
        if (result.Errors is not null)
        {
            foreach (var (key, messages) in result.Errors)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(key, message);
                }
            }

            return Page();
        }

        TempData[nameof(IndexModel.ConfirmationMessage)] =
            $"{Template.Name} was submitted successfully.";
        return RedirectToPage(
            "/Organizations/Surveys/Index",
            new { organizationId });
    }

    private async Task<bool> LoadPageAsync(
        int organizationId,
        int templateId,
        CancellationToken cancellationToken)
    {
        var organizationName = await OrganizationQueries.GetNameAsync(
            dbContext,
            organizationId,
            cancellationToken);
        var template = await SurveyTemplateQueries.GetActiveAsync(
            dbContext,
            templateId,
            cancellationToken);
        if (organizationName is null || template is null)
        {
            return false;
        }

        var dueTemplates = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organizationId,
            timeProvider.GetUtcNow().UtcDateTime.Date,
            cancellationToken);
        if (!dueTemplates.Any(due => due.TemplateId == templateId))
        {
            return false;
        }

        OrganizationId = organizationId;
        OrganizationName = organizationName;
        Template = template;
        return true;
    }
}

public sealed class SurveyFormInput
{
    public string? Label { get; set; }
    public DateTime? SnapshotDate { get; set; }
    public string? Notes { get; set; }
    public List<SurveyAnswerInput> Answers { get; set; } = [];
}

public sealed class SurveyAnswerInput
{
    public int SurveyQuestionId { get; set; }
    public SurveyResponseLevel? ResponseLevel { get; set; }
}

public sealed record SurveyResponseOption(
    SurveyResponseLevel Value,
    string Label);
