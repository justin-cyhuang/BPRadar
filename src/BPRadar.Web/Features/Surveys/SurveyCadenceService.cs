using BPRadar.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Surveys;

public static class SurveyCadenceService
{
    private const int DueSoonWindowDays = 14;

    public static async Task<DueSurveyTemplate[]> ListDueAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        DateTime currentDate,
        CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.SurveyTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.Name)
            .Select(template => new
            {
                TemplateId = template.Id,
                template.Name,
                template.Description,
                template.Cadence,
                QuestionCount = template.Questions.Count
            })
            .ToArrayAsync(cancellationToken);
        var latestSnapshots = await dbContext.SurveySubmissions
            .AsNoTracking()
            .Where(submission => submission.OrganizationId == organizationId)
            .GroupBy(submission => submission.SurveyTemplateId)
            .Select(group => new
            {
                TemplateId = group.Key,
                SnapshotDate = group.Max(submission => submission.SnapshotDate)
            })
            .ToDictionaryAsync(
                item => item.TemplateId,
                item => item.SnapshotDate,
                cancellationToken);
        var today = currentDate.Date;

        return templates
            .Select(template =>
            {
                var hasSubmission = latestSnapshots.TryGetValue(
                    template.TemplateId,
                    out var latestSnapshot);
                var nextDueDate = hasSubmission
                    ? AddCadence(latestSnapshot, template.Cadence)
                    : (DateTime?)null;
                return new DueSurveyTemplate(
                    template.TemplateId,
                    template.Name,
                    template.Description,
                    template.Cadence,
                    template.QuestionCount,
                    hasSubmission ? latestSnapshot : null,
                    nextDueDate,
                    GetStatus(today, nextDueDate));
            })
            .Where(template => template.Status != SurveyDueStatus.OnTime)
            .ToArray();
    }

    private static SurveyDueStatus GetStatus(
        DateTime today,
        DateTime? nextDueDate)
    {
        if (nextDueDate is null || nextDueDate < today)
        {
            return SurveyDueStatus.Overdue;
        }

        return nextDueDate <= today.AddDays(DueSoonWindowDays)
            ? SurveyDueStatus.DueSoon
            : SurveyDueStatus.OnTime;
    }

    private static DateTime AddCadence(DateTime snapshotDate, SurveyCadence cadence) =>
        cadence switch
        {
            SurveyCadence.Monthly => snapshotDate.AddMonths(1),
            SurveyCadence.Quarterly => snapshotDate.AddMonths(3),
            SurveyCadence.SemiAnnual => snapshotDate.AddMonths(6),
            SurveyCadence.Annual => snapshotDate.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(
                nameof(cadence),
                cadence,
                "Unsupported survey cadence.")
        };
}

public sealed record DueSurveyTemplate(
    int TemplateId,
    string Name,
    string? Description,
    SurveyCadence Cadence,
    int QuestionCount,
    DateTime? LastSubmittedSnapshotDate,
    DateTime? NextDueDate,
    SurveyDueStatus Status);

public enum SurveyDueStatus
{
    OnTime,
    DueSoon,
    Overdue
}
