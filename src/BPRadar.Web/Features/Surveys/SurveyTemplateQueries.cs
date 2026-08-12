using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Surveys;

public static class SurveyTemplateQueries
{
    public static async Task<SurveyTemplateSummary[]> ListAsync(
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        await dbContext.SurveyTemplates
            .AsNoTracking()
            .OrderBy(template => template.Name)
            .Select(template => new SurveyTemplateSummary(
                template.Id,
                template.Name,
                template.Framework!.Name,
                template.Cadence.ToString(),
                template.IsActive,
                template.Questions.Count))
            .ToArrayAsync(cancellationToken);

    public static async Task<SurveyTemplateDetail?> GetActiveAsync(
        BPRadarDbContext dbContext,
        int templateId,
        CancellationToken cancellationToken = default) =>
        await dbContext.SurveyTemplates
            .AsNoTracking()
            .Where(template => template.Id == templateId && template.IsActive)
            .Select(template => new SurveyTemplateDetail(
               template.Id,
               template.Name,
               template.Description,
               template.Cadence.ToString(),
               template.IsActive,
               template.Questions
                   .OrderBy(question => question.SortOrder)
                   .Select(question => new SurveyQuestionDetail(
                       question.Id,
                       question.Code,
                       question.Prompt,
                       question.SortOrder,
                       question.IsRequired))
                   .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed record SurveyTemplateSummary(
    int Id,
    string Name,
    string Framework,
    string Cadence,
    bool IsActive,
    int QuestionCount);

public sealed record SurveyTemplateDetail(
    int Id,
    string Name,
    string? Description,
    string Cadence,
    bool IsActive,
    SurveyQuestionDetail[] Questions);

public sealed record SurveyQuestionDetail(
    int Id,
    string Code,
    string Prompt,
    int SortOrder,
    bool IsRequired);
