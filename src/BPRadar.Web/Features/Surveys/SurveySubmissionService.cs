using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BPRadar.Web.Features.Surveys;

public static class SurveySubmissionService
{
    public static async Task<SurveySubmissionCreateResult> CreateAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        CreateSurveySubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Surveys",
            "SurveySubmissionStarted",
            $"organizationId={organizationId} surveyTemplateId={request.SurveyTemplateId}");

        if (!await dbContext.Organizations.AnyAsync(
                organization => organization.Id == organizationId,
                cancellationToken))
        {
            return SurveySubmissionCreateResult.Invalid(
                "OrganizationId",
                $"Organization {organizationId} does not exist.");
        }

        var template = await dbContext.SurveyTemplates
            .Include(item => item.Questions)
            .SingleOrDefaultAsync(
                item => item.Id == request.SurveyTemplateId && item.IsActive,
                cancellationToken);
        if (template is null)
        {
            return SurveySubmissionCreateResult.Invalid(
                "SurveyTemplateId",
                $"Active survey template {request.SurveyTemplateId} does not exist.");
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return SurveySubmissionCreateResult.Invalid(
                "Label",
                "A submission label is required.");
        }

        var snapshotDate = request.SnapshotDate.Date;
        if (request.SnapshotDate == default ||
            snapshotDate > DateTime.UtcNow.Date)
        {
            return SurveySubmissionCreateResult.Invalid(
                "SnapshotDate",
                "Snapshot date must be valid and cannot be later than the current UTC date.");
        }

        if (await dbContext.SurveySubmissions.AnyAsync(
                submission =>
                    submission.OrganizationId == organizationId &&
                    submission.SurveyTemplateId == request.SurveyTemplateId &&
                    submission.SnapshotDate == snapshotDate,
                cancellationToken))
        {
            return SurveySubmissionCreateResult.Invalid(
                "SnapshotDate",
                DuplicateSnapshotMessage(snapshotDate));
        }

        var answers = request.Answers ?? [];
        var duplicateQuestionIds = answers
            .GroupBy(answer => answer.SurveyQuestionId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToArray();
        if (duplicateQuestionIds.Length > 0)
        {
            return SurveySubmissionCreateResult.Invalid(
                "Answers",
                $"Questions can only be answered once: {string.Join(", ", duplicateQuestionIds)}.");
        }

        var templateQuestionIds = template.Questions
            .Select(question => question.Id)
            .ToHashSet();
        var invalidQuestionIds = answers
            .Select(answer => answer.SurveyQuestionId)
            .Where(questionId => !templateQuestionIds.Contains(questionId))
            .Distinct()
            .Order()
            .ToArray();
        if (invalidQuestionIds.Length > 0)
        {
            return SurveySubmissionCreateResult.Invalid(
                "Answers",
                $"Questions do not belong to the active template: {string.Join(", ", invalidQuestionIds)}.");
        }

        var answeredQuestionIds = answers
            .Select(answer => answer.SurveyQuestionId)
            .ToHashSet();
        var missingRequiredQuestions = template.Questions
            .Where(question =>
                question.IsRequired &&
                !answeredQuestionIds.Contains(question.Id))
            .OrderBy(question => question.SortOrder)
            .Select(question => question.Code)
            .ToArray();
        if (missingRequiredQuestions.Length > 0)
        {
            return SurveySubmissionCreateResult.Invalid(
                "Answers",
                $"Required questions must be answered: {string.Join(", ", missingRequiredQuestions)}.");
        }

        var invalidResponseLevels = answers
            .Where(answer =>
                !Enum.TryParse<SurveyResponseLevel>(
                    answer.ResponseLevel,
                    ignoreCase: true,
                    out var level) ||
                !Enum.IsDefined(level) ||
                int.TryParse(answer.ResponseLevel, out _))
            .Select(answer => answer.ResponseLevel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalidResponseLevels.Length > 0)
        {
            return SurveySubmissionCreateResult.Invalid(
                "Answers",
                $"Invalid SurveyResponseLevel values: {string.Join(", ", invalidResponseLevels)}.");
        }

        var questionsById = template.Questions.ToDictionary(question => question.Id);
        var now = DateTime.UtcNow;
        var submission = new SurveySubmission
        {
            OrganizationId = organizationId,
            SurveyTemplate = template,
            Label = request.Label,
            SnapshotDate = snapshotDate,
            SubmittedAt = now,
            Notes = request.Notes
        };

        foreach (var answer in answers)
        {
            submission.Responses.Add(new SurveyResponse
            {
                SurveyQuestion = questionsById[answer.SurveyQuestionId],
                ResponseLevel = Enum.Parse<SurveyResponseLevel>(
                    answer.ResponseLevel,
                    ignoreCase: true),
                Score = answer.Score,
                Notes = answer.Notes
            });
        }

        dbContext.SurveySubmissions.Add(submission);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteExtendedErrorCode: 2067
            })
        {
            return SurveySubmissionCreateResult.Invalid(
                "SnapshotDate",
                DuplicateSnapshotMessage(snapshotDate));
        }

        var traceDetails =
            $"organizationId={organizationId} surveyTemplateId={template.Id} " +
            $"surveySubmissionId={submission.Id} responses={submission.Responses.Count}";
        BPRadarTrace.Write(
            TraceEventType.Information,
            "Surveys",
            "SurveySubmissionSaved",
            traceDetails);
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Surveys",
            "SurveySubmissionFinalized",
            traceDetails);
        return SurveySubmissionCreateResult.Success(ToDetail(submission));
    }

    public static async Task<SurveySubmissionDetail[]> GetLatestAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        var submissions = await dbContext.SurveySubmissions
            .AsNoTracking()
            .Where(submission => submission.OrganizationId == organizationId)
            .Include(submission => submission.SurveyTemplate)
            .Include(submission => submission.Responses)
            .ThenInclude(response => response.SurveyQuestion)
            .OrderByDescending(submission => submission.SnapshotDate)
            .ThenByDescending(submission => submission.SubmittedAt)
            .ToListAsync(cancellationToken);

        return submissions
            .GroupBy(submission => submission.SurveyTemplateId)
            .Select(group => ToDetail(group.First()))
            .OrderBy(submission => submission.SurveyTemplateName)
            .ToArray();
    }

    public static async Task<SurveySubmissionReview?> GetReviewAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int submissionId,
        CancellationToken cancellationToken = default)
    {
        var submission = await dbContext.SurveySubmissions
            .AsNoTracking()
            .Include(item => item.Organization)
            .Include(item => item.SurveyTemplate)
            .Include(item => item.Responses)
            .ThenInclude(response => response.SurveyQuestion)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == submissionId &&
                    item.OrganizationId == organizationId,
                cancellationToken);
        if (submission is null)
        {
            return null;
        }

        return new SurveySubmissionReview(
            submission.Id,
            submission.OrganizationId,
            submission.Organization.Name,
            submission.SurveyTemplateId,
            submission.SurveyTemplate.Name,
            submission.Label,
            submission.SnapshotDate,
            submission.SubmittedAt,
            submission.Notes,
            SurveyScoringService.CalculateProfileScore(submission.Responses),
            submission.Responses
                .OrderBy(response => response.SurveyQuestion.SortOrder)
                .ThenBy(response => response.SurveyQuestion.Code)
                .Select(response => new SurveyResponseReview(
                    response.SurveyQuestion.Code,
                    response.SurveyQuestion.Prompt,
                    response.ResponseLevel,
                    SurveyScoringService.CalculateResponseScore(response),
                    response.Notes))
                .ToArray());
    }

    private static SurveySubmissionDetail ToDetail(SurveySubmission submission) =>
        new(
            submission.Id,
            submission.OrganizationId,
            submission.SurveyTemplateId,
            submission.SurveyTemplate.Name,
            submission.Label,
            submission.SnapshotDate,
            submission.SubmittedAt,
            submission.Notes,
            submission.Responses
                .OrderBy(response => response.SurveyQuestion.SortOrder)
                .Select(response => new SurveyResponseDetail(
                    response.SurveyQuestionId,
                    response.SurveyQuestion.Code,
                    response.ResponseLevel.ToString(),
                    response.Score,
                    response.Notes))
                .ToArray());

    private static string DuplicateSnapshotMessage(DateTime snapshotDate) =>
        $"A survey response was already submitted for this snapshot date ({snapshotDate:yyyy-MM-dd}).";
}

public sealed record CreateSurveySubmissionRequest(
    int SurveyTemplateId,
    string Label,
    DateTime SnapshotDate,
    string? Notes,
    SurveyAnswerRequest[]? Answers);

public sealed record SurveyAnswerRequest(
    int SurveyQuestionId,
    string ResponseLevel,
    decimal? Score,
    string? Notes);

public sealed record SurveySubmissionDetail(
    int Id,
    int OrganizationId,
    int SurveyTemplateId,
    string SurveyTemplateName,
    string Label,
    DateTime SnapshotDate,
    DateTime SubmittedAt,
    string? Notes,
    SurveyResponseDetail[] Responses);

public sealed record SurveyResponseDetail(
    int SurveyQuestionId,
    string QuestionCode,
    string ResponseLevel,
    decimal? Score,
    string? Notes);

public sealed record SurveySubmissionReview(
    int Id,
    int OrganizationId,
    string OrganizationName,
    int SurveyTemplateId,
    string SurveyTemplateName,
    string Label,
    DateTime SnapshotDate,
    DateTime SubmittedAt,
    string? Notes,
    decimal? ProfileScore,
    SurveyResponseReview[] Responses);

public sealed record SurveyResponseReview(
    string QuestionCode,
    string QuestionPrompt,
    SurveyResponseLevel ResponseLevel,
    decimal? Score,
    string? Notes);

public sealed record SurveySubmissionCreateResult(
    SurveySubmissionDetail? Submission,
    Dictionary<string, string[]>? Errors)
{
    public static SurveySubmissionCreateResult Success(
        SurveySubmissionDetail submission) =>
        new(submission, null);

    public static SurveySubmissionCreateResult Invalid(string key, string message) =>
        new(null, new Dictionary<string, string[]> { [key] = [message] });
}
