using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Dashboard;

internal static class DashboardSurveyTrackingBuilder
{
    public static async Task<DashboardSurveyResult> BuildAsync(
        BPRadarDbContext dbContext,
        DashboardRequest request,
        DateTime? surveySubmissionFrom,
        DateTime? surveySubmissionTo,
        CancellationToken cancellationToken = default)
    {
        var templateOptions = await dbContext.SurveyTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.Name)
            .Select(template => new DashboardSurveyTemplateOption(
                template.Id,
                template.Name,
                template.Cadence))
            .ToArrayAsync(cancellationToken);
        var selectedTemplateId = request.SurveyTemplateId is not null &&
            templateOptions.Any(option => option.Id == request.SurveyTemplateId)
                ? request.SurveyTemplateId
                : null;
        if (selectedTemplateId is null)
        {
            return new DashboardSurveyResult(null, templateOptions, null);
        }

        var cadence = await SurveyCadenceService.GetStatusAsync(
            dbContext,
            request.OrganizationId,
            selectedTemplateId.Value,
            request.CurrentDate?.Date ?? DateTime.UtcNow.Date,
            cancellationToken);
        IQueryable<SurveySubmission> submissionQuery = dbContext.SurveySubmissions
            .AsNoTracking()
            .Where(submission =>
                submission.OrganizationId == request.OrganizationId &&
                submission.SurveyTemplateId == selectedTemplateId)
            .Include(submission => submission.Responses)
            .ThenInclude(response => response.SurveyQuestion)
            .ThenInclude(question => question.Domain);
        if (surveySubmissionFrom is not null)
        {
            submissionQuery = submissionQuery.Where(submission =>
                submission.SnapshotDate >= surveySubmissionFrom);
        }

        if (surveySubmissionTo is not null)
        {
            submissionQuery = submissionQuery.Where(submission =>
                submission.SnapshotDate <= surveySubmissionTo);
        }

        var submissions = await submissionQuery
            .OrderByDescending(submission => submission.SnapshotDate)
            .ThenByDescending(submission => submission.SubmittedAt)
            .ThenByDescending(submission => submission.Id)
            .ToArrayAsync(cancellationToken);
        var scoredSubmissions = submissions
            .Select(submission => new
            {
                Submission = submission,
                Score = SurveyScoringService.CalculateProfileScore(
                    submission.Responses)
            })
            .ToArray();
        var history = scoredSubmissions
            .Select((item, index) =>
            {
                var previousScore = index + 1 < scoredSubmissions.Length
                    ? scoredSubmissions[index + 1].Score
                    : null;
                var delta = item.Score is not null && previousScore is not null
                    ? item.Score.Value - previousScore.Value
                    : (decimal?)null;
                return new SurveyHistoryItem(
                    item.Submission.Id,
                    item.Submission.Label,
                    item.Submission.SnapshotDate,
                    item.Score,
                    delta,
                    item.Submission.Notes);
            })
            .ToArray();
        var trend = history
            .Where(item => item.Score is not null)
            .OrderBy(item => item.SnapshotDate)
            .ThenBy(item => item.SubmissionId)
            .Select(item => new SurveyTrendPoint(
                item.SnapshotDate,
                item.Score!.Value))
            .ToArray();
        var domainDeltas = BuildDomainDeltas(submissions);
        var tracking = new SurveyTracking(
            cadence!.TemplateId,
            cadence.Name,
            scoredSubmissions.FirstOrDefault()?.Score,
            history.FirstOrDefault()?.Delta,
            cadence.Status,
            cadence.NextDueDate,
            history,
            trend,
            domainDeltas);

        return new DashboardSurveyResult(
            selectedTemplateId,
            templateOptions,
            tracking);
    }

    private static SurveyDomainDelta[] BuildDomainDeltas(
        SurveySubmission[] submissions)
    {
        if (submissions.Length < 2)
        {
            return [];
        }

        var latestDomainScores = SurveyScoringService.CalculateDomainScores(
            submissions[0].Responses);
        var previousDomainScores = SurveyScoringService.CalculateDomainScores(
            submissions[1].Responses);
        return submissions[0].Responses
            .Where(response => response.SurveyQuestion.Domain is not null)
            .Select(response => response.SurveyQuestion.Domain!)
            .DistinctBy(domain => domain.Id)
            .Where(domain =>
                latestDomainScores.ContainsKey(domain.Id) &&
                previousDomainScores.ContainsKey(domain.Id))
            .OrderBy(domain => domain.SortOrder)
            .ThenBy(domain => domain.Code)
            .Select(domain => new SurveyDomainDelta(
                domain.Id,
                domain.Code,
                domain.Name,
                latestDomainScores[domain.Id],
                previousDomainScores[domain.Id],
                latestDomainScores[domain.Id] -
                    previousDomainScores[domain.Id]))
            .ToArray();
    }
}

internal sealed record DashboardSurveyResult(
    int? SelectedSurveyTemplateId,
    DashboardSurveyTemplateOption[] TemplateOptions,
    SurveyTracking? Tracking);
