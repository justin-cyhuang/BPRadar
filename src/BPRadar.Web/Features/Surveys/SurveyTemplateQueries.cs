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
                template.Name,
                template.Framework!.Name,
                template.Cadence.ToString(),
                template.Questions.Count))
            .ToArrayAsync(cancellationToken);
}

public sealed record SurveyTemplateSummary(
    string Name,
    string Framework,
    string Cadence,
    int QuestionCount);
