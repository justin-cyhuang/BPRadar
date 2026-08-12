using System.Net;
using System.Net.Http.Json;
using BPRadar.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests;

[TestClass]
public sealed class SurveySubmissionEndpointTests
{
    [TestMethod]
    public async Task Admin_can_open_an_active_template_with_its_required_questions()
    {
        await using var application = SurveyApplication.Create();
        using var client = application.CreateClient();
        var template = await GetActiveTemplateAsync(
            client,
            "Azure WAF Transformation Pulse");

        using var response = await client.GetAsync(
            $"/api/admin/survey-templates/{template.Id}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<SurveyTemplateDetail>();
        Assert.IsNotNull(detail);
        Assert.AreEqual(template.Id, detail.Id);
        Assert.AreEqual("Azure WAF Transformation Pulse", detail.Name);
        Assert.IsTrue(detail.IsActive);
        Assert.HasCount(20, detail.Questions);
        Assert.IsTrue(detail.Questions.Any(question => question.IsRequired));
        CollectionAssert.AreEqual(
            detail.Questions.Select(question => question.SortOrder).Order().ToArray(),
            detail.Questions.Select(question => question.SortOrder).ToArray());
    }

    [TestMethod]
    public async Task Admin_can_submit_answers_and_retrieve_the_organizations_latest_response()
    {
        await using var application = SurveyApplication.Create();
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var template = await GetActiveTemplateAsync(
            client,
            "Azure WAF Transformation Pulse");
        var snapshotDate = DateTime.UtcNow.Date.AddDays(-1);
        var submission = new CreateSurveySubmissionRequest(
            template.Id,
            "2026 Q3 WAF pulse",
            snapshotDate,
            "Quarterly transformation check-in",
            template.Questions
                .Select(question => new SurveyAnswerRequest(
                    question.Id,
                    "High",
                    $"Answer for {question.Code}"))
                .ToArray());

        using var submitResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/survey-submissions",
            submission);

        Assert.AreEqual(HttpStatusCode.Created, submitResponse.StatusCode);
        var created = await submitResponse.Content
            .ReadFromJsonAsync<SurveySubmissionDetail>();
        Assert.IsNotNull(created);
        Assert.AreEqual(organizationId, created.OrganizationId);
        Assert.AreEqual(template.Id, created.SurveyTemplateId);
        Assert.AreEqual(snapshotDate, created.SnapshotDate);
        Assert.HasCount(20, created.Responses);
        Assert.IsTrue(created.Responses.All(answer => answer.ResponseLevel == "High"));

        var newerSubmission = submission with
        {
            Label = "2026 Q3 WAF pulse refresh",
            SnapshotDate = DateTime.UtcNow.Date,
            Answers = template.Questions
                .Select(question => new SurveyAnswerRequest(
                    question.Id,
                    "VeryHigh",
                    null))
                .ToArray()
        };
        using var newerResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/survey-submissions",
            newerSubmission);
        Assert.AreEqual(HttpStatusCode.Created, newerResponse.StatusCode);
        var newer = await newerResponse.Content
            .ReadFromJsonAsync<SurveySubmissionDetail>();
        Assert.IsNotNull(newer);

        var latest = await client.GetFromJsonAsync<SurveySubmissionDetail[]>(
            $"/api/organizations/{organizationId}/survey-submissions/latest");

        Assert.IsNotNull(latest);
        Assert.HasCount(1, latest);
        Assert.AreEqual(newer.Id, latest[0].Id);
        Assert.AreEqual(DateTime.UtcNow.Date, latest[0].SnapshotDate);
        Assert.HasCount(20, latest[0].Responses);
        Assert.IsTrue(
            latest[0].Responses.All(answer => answer.ResponseLevel == "VeryHigh"));
    }

    [TestMethod]
    public async Task Submission_rejects_an_unanswered_required_question()
    {
        await using var application = SurveyApplication.Create();
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var template = await GetActiveTemplateAsync(
            client,
            "Azure WAF Transformation Pulse");
        var omittedQuestion = template.Questions.First(question => question.IsRequired);
        var submission = new CreateSurveySubmissionRequest(
            template.Id,
            "Incomplete pulse",
            DateTime.UtcNow.Date,
            null,
            template.Questions
                .Where(question => question.Id != omittedQuestion.Id)
                .Select(question => new SurveyAnswerRequest(
                    question.Id,
                    "Medium",
                    null))
                .ToArray());

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/survey-submissions",
            submission);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.IsNotNull(problem);
        Assert.IsTrue(problem.Errors.ContainsKey("Answers"));
        StringAssert.Contains(problem.Errors["Answers"].Single(), omittedQuestion.Code);
        var latest = await client.GetFromJsonAsync<SurveySubmissionDetail[]>(
            $"/api/organizations/{organizationId}/survey-submissions/latest");
        Assert.IsNotNull(latest);
        Assert.IsEmpty(latest);
    }

    [TestMethod]
    public async Task Submission_rejects_an_answer_for_a_question_outside_the_active_template()
    {
        await using var application = SurveyApplication.Create();
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var template = await GetActiveTemplateAsync(
            client,
            "Azure WAF Transformation Pulse");
        var otherTemplate = await GetActiveTemplateAsync(
            client,
            "ISO 27001 ISMS Transformation Pulse");
        var invalidQuestion = otherTemplate.Questions[0];
        var answers = template.Questions
            .Select(question => new SurveyAnswerRequest(
                question.Id,
                "Medium",
                null))
            .Append(new SurveyAnswerRequest(
                invalidQuestion.Id,
                "Medium",
                null))
            .ToArray();
        var submission = new CreateSurveySubmissionRequest(
            template.Id,
            "Cross-template pulse",
            DateTime.UtcNow.Date,
            null,
            answers);

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/survey-submissions",
            submission);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.IsNotNull(problem);
        StringAssert.Contains(
            problem.Errors["Answers"].Single(),
            invalidQuestion.Id.ToString());
        var latest = await client.GetFromJsonAsync<SurveySubmissionDetail[]>(
            $"/api/organizations/{organizationId}/survey-submissions/latest");
        Assert.IsNotNull(latest);
        Assert.IsEmpty(latest);
    }

    [TestMethod]
    public async Task Submission_rejects_an_answer_outside_the_response_level_scale()
    {
        await using var application = SurveyApplication.Create();
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var template = await GetActiveTemplateAsync(
            client,
            "Azure WAF Transformation Pulse");
        var answers = template.Questions
            .Select(question => new SurveyAnswerRequest(
                question.Id,
                question == template.Questions[0] ? "Excellent" : "High",
                null))
            .ToArray();
        var submission = new CreateSurveySubmissionRequest(
            template.Id,
            "Invalid scale pulse",
            DateTime.UtcNow.Date,
            null,
            answers);

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/survey-submissions",
            submission);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.IsNotNull(problem);
        StringAssert.Contains(
            problem.Errors["Answers"].Single(),
            "Excellent");
    }

    private static async Task<SurveyTemplateDetail> GetActiveTemplateAsync(
        HttpClient client,
        string name)
    {
        var templates = await client.GetFromJsonAsync<SurveyTemplateSummary[]>(
            "/api/admin/survey-templates");
        Assert.IsNotNull(templates);
        var summary = templates.Single(template =>
            template.Name == name && template.IsActive);
        var detail = await client.GetFromJsonAsync<SurveyTemplateDetail>(
            $"/api/admin/survey-templates/{summary.Id}");
        Assert.IsNotNull(detail);
        return detail;
    }

    private sealed record SurveyTemplateSummary(
        int Id,
        string Name,
        bool IsActive);

    private sealed record SurveyTemplateDetail(
        int Id,
        string Name,
        bool IsActive,
        SurveyQuestionDetail[] Questions);

    private sealed record SurveyQuestionDetail(
        int Id,
        string Code,
        string Prompt,
        int SortOrder,
        bool IsRequired);

    private sealed record CreateSurveySubmissionRequest(
        int SurveyTemplateId,
        string Label,
        DateTime SnapshotDate,
        string? Notes,
        SurveyAnswerRequest[] Answers);

    private sealed record SurveyAnswerRequest(
        int SurveyQuestionId,
        string ResponseLevel,
        string? Notes);

    private sealed record SurveySubmissionDetail(
        int Id,
        int OrganizationId,
        int SurveyTemplateId,
        string SurveyTemplateName,
        string Label,
        DateTime SnapshotDate,
        DateTime SubmittedAt,
        string? Notes,
        SurveyResponseDetail[] Responses);

    private sealed record SurveyResponseDetail(
        int SurveyQuestionId,
        string QuestionCode,
        string ResponseLevel,
        string? Notes);

    private sealed class SurveyApplication(
        string databasePath,
        WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        private readonly string databasePath = databasePath;
        private readonly WebApplicationFactory<Program> factory = factory;

        public static SurveyApplication Create()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-survey-{Guid.NewGuid():N}.db");
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
                }));
            return new SurveyApplication(databasePath, factory);
        }

        public HttpClient CreateClient() => factory.CreateClient();

        public async Task<int> CreateOrganizationAsync(string name)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            var organization = new Organization { Name = name };
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync();
            return organization.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await factory.DisposeAsync();
            File.Delete(databasePath);
        }
    }
}
