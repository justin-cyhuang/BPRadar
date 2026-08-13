using System.Net;
using System.Net.Http.Json;
using BPRadar.Web.Data;
using BPRadar.Web.Features.IssueMatching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests;

[TestClass]
public sealed class IssueLifecycleEndpointTests
{
    [TestMethod]
    public async Task Admin_can_record_an_issue_before_running_matching()
    {
        await using var application = IssueApplication.Create(
            new StubIssueMatchingService());
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/issues",
            new CreateIssueRequest(
                "Checkout outage",
                "Customers could not complete purchases.",
                "A single point of failure existed in the payment tier."));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var issue = await response.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(issue);
        Assert.AreEqual("Pending", issue.MatchingStatus);
        Assert.AreEqual("A single point of failure existed in the payment tier.", issue.RootCause);
        Assert.IsNull(issue.MatchedAt);
        Assert.IsEmpty(issue.ViolationMatches);
    }

    [TestMethod]
    public async Task Running_matching_persists_candidates_and_indicates_no_survey_response()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.94m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var issue = await CreateIssueAsync(client, organizationId);

        using var response = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var matchedIssue = await response.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(matchedIssue);
        Assert.AreEqual("Matched", matchedIssue.MatchingStatus);
        Assert.IsNotNull(matchedIssue.MatchedAt);
        Assert.IsNull(matchedIssue.MatchingError);
        Assert.HasCount(1, matchedIssue.ViolationMatches);
        var match = matchedIssue.ViolationMatches[0];
        Assert.AreEqual("RE:05", match.ControlCode);
        Assert.AreEqual(0.94m, match.MatchScore);
        Assert.AreEqual("Open", match.ReviewStatus);
        Assert.AreEqual("NoSurveyResponse", match.DiscrepancyStatus);
        Assert.IsNull(match.SelfReportedState);
        Assert.IsFalse(match.IsSelfAssessmentDiscrepancy);
    }

    [TestMethod]
    public async Task High_self_reported_state_is_flagged_as_a_discrepancy()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.94m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        await SubmitWafSurveyAsync(client, organizationId, "High");
        var issue = await CreateIssueAsync(client, organizationId);

        var matchedIssue = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        matchedIssue.EnsureSuccessStatusCode();
        var detail = await matchedIssue.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(detail);
        var match = detail.ViolationMatches.Single();
        Assert.AreEqual("High", match.SelfReportedState);
        Assert.AreEqual("Discrepancy", match.DiscrepancyStatus);
        Assert.IsTrue(match.IsSelfAssessmentDiscrepancy);
    }

    [TestMethod]
    public async Task Provider_failure_is_persisted_and_can_be_retried()
    {
        var matchingService = new StubIssueMatchingService
        {
            Failure = new HttpRequestException("Provider unavailable.")
        };
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var issue = await CreateIssueAsync(client, organizationId);

        using var failedResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        Assert.AreEqual(HttpStatusCode.BadGateway, failedResponse.StatusCode);
        var failedIssue = await client.GetFromJsonAsync<IssueDetail>(
            $"/api/issues/{issue.Id}");
        Assert.IsNotNull(failedIssue);
        Assert.AreEqual("Failed", failedIssue.MatchingStatus);
        Assert.AreEqual("Provider unavailable.", failedIssue.MatchingError);
        Assert.IsNotNull(failedIssue.MatchedAt);

        matchingService.Failure = null;
        matchingService.Result = new IssueMatchResult([], []);
        using var retryResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        Assert.AreEqual(HttpStatusCode.OK, retryResponse.StatusCode);
        var retriedIssue = await retryResponse.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(retriedIssue);
        Assert.AreEqual("Matched", retriedIssue.MatchingStatus);
        Assert.IsNull(retriedIssue.MatchingError);
    }

    [TestMethod]
    public async Task Admin_can_list_issues_and_review_a_violation_match()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.94m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var issue = await CreateIssueAsync(client, organizationId);
        using var matchingResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        matchingResponse.EnsureSuccessStatusCode();
        var matchedIssue = await matchingResponse.Content
            .ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(matchedIssue);
        var match = matchedIssue.ViolationMatches.Single();

        using var reviewResponse = await client.PutAsJsonAsync(
            $"/api/violation-matches/{match.Id}/review",
            new { reviewStatus = "Confirmed" });

        Assert.AreEqual(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewedMatch = await reviewResponse.Content
            .ReadFromJsonAsync<ViolationMatchDetail>();
        Assert.IsNotNull(reviewedMatch);
        Assert.AreEqual("Confirmed", reviewedMatch.ReviewStatus);
        var issues = await client.GetFromJsonAsync<IssueDetail[]>(
            $"/api/organizations/{organizationId}/issues");
        Assert.IsNotNull(issues);
        Assert.HasCount(1, issues);
        Assert.AreEqual("Confirmed", issues[0].ViolationMatches[0].ReviewStatus);
    }

    [TestMethod]
    public async Task Root_cause_can_be_added_before_matching()
    {
        await using var application = IssueApplication.Create(
            new StubIssueMatchingService());
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        using var createResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/issues",
            new CreateIssueRequest(
                "Checkout outage",
                "Customers could not complete purchases.",
                null));
        createResponse.EnsureSuccessStatusCode();
        var issue = await createResponse.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(issue);

        using var blockedResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        Assert.AreEqual(HttpStatusCode.BadRequest, blockedResponse.StatusCode);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/issues/{issue.Id}",
            new CreateIssueRequest(
                issue.Title,
                issue.Description,
                "A single point of failure existed."));
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        using var matchingResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        Assert.AreEqual(HttpStatusCode.OK, matchingResponse.StatusCode);
    }

    [TestMethod]
    public async Task Acknowledged_weakness_is_not_flagged_as_a_discrepancy()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.94m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        await SubmitWafSurveyAsync(client, organizationId, "Medium");
        var issue = await CreateIssueAsync(client, organizationId);

        using var response = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(detail);
        var match = detail.ViolationMatches.Single();
        Assert.AreEqual("Medium", match.SelfReportedState);
        Assert.AreEqual("NoDiscrepancy", match.DiscrepancyStatus);
        Assert.IsFalse(match.IsSelfAssessmentDiscrepancy);
    }

    [TestMethod]
    public async Task Rerunning_matching_updates_the_existing_violation_match()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.80m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var issue = await CreateIssueAsync(client, organizationId);
        using var firstResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        firstResponse.EnsureSuccessStatusCode();

        matchingService.Result = new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.97m)
            ]);
        using var secondResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        secondResponse.EnsureSuccessStatusCode();
        var detail = await secondResponse.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(detail);
        Assert.HasCount(1, detail.ViolationMatches);
        Assert.AreEqual(0.97m, detail.ViolationMatches[0].MatchScore);
    }

    [TestMethod]
    public async Task Rerunning_matching_removes_stale_open_candidates()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.80m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var issue = await CreateIssueAsync(client, organizationId);
        using var firstResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        firstResponse.EnsureSuccessStatusCode();

        matchingService.Result = new IssueMatchResult([], []);
        using var secondResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);

        secondResponse.EnsureSuccessStatusCode();
        var detail = await secondResponse.Content.ReadFromJsonAsync<IssueDetail>();
        Assert.IsNotNull(detail);
        Assert.IsEmpty(detail.ViolationMatches);
    }

    [TestMethod]
    public async Task Admin_can_open_the_dedicated_issues_page()
    {
        await using var application = IssueApplication.Create(
            new StubIssueMatchingService());
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");

        var html = await client.GetStringAsync(
            $"/Admin/Issues?organizationId={organizationId}");

        StringAssert.Contains(html, "Issue entry and violation matching");
        StringAssert.Contains(html, "Contoso");
        StringAssert.Contains(html, "Run matching");
        StringAssert.Contains(html, "Confirm");
        StringAssert.Contains(html, "Dismiss");
    }

    [TestMethod]
    public async Task Survey_added_after_matching_updates_discrepancy_and_priority_on_read()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy", "backup failure"],
            [
                new ControlMatchCandidate(
                    "ISO27001_2022",
                    "A.8.13",
                    ["backup failure"],
                    ["backup"],
                    0.99m),
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.80m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        var issue = await CreateIssueAsync(client, organizationId);
        using var matchingResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        matchingResponse.EnsureSuccessStatusCode();
        await SubmitWafSurveyAsync(client, organizationId, "High");

        var detail = await client.GetFromJsonAsync<IssueDetail>(
            $"/api/issues/{issue.Id}");

        Assert.IsNotNull(detail);
        Assert.AreEqual("RE:05", detail.ViolationMatches[0].ControlCode);
        Assert.AreEqual("High", detail.ViolationMatches[0].SelfReportedState);
        Assert.AreEqual(
            "Discrepancy",
            detail.ViolationMatches[0].DiscrepancyStatus);
        Assert.IsTrue(
            detail.ViolationMatches[0].IsSelfAssessmentDiscrepancy);
    }

    [TestMethod]
    public async Task Lower_survey_response_after_matching_clears_discrepancy_on_read()
    {
        var matchingService = new StubIssueMatchingService(new IssueMatchResult(
            ["missing redundancy"],
            [
                new ControlMatchCandidate(
                    "AZURE_WAF",
                    "RE:05",
                    ["missing redundancy"],
                    ["redundancy"],
                    0.94m)
            ]));
        await using var application = IssueApplication.Create(matchingService);
        using var client = application.CreateClient();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        await SubmitWafSurveyAsync(
            client,
            organizationId,
            "High",
            DateTime.UtcNow.Date.AddDays(-1));
        var issue = await CreateIssueAsync(client, organizationId);
        using var matchingResponse = await client.PostAsync(
            $"/api/issues/{issue.Id}/matching",
            content: null);
        matchingResponse.EnsureSuccessStatusCode();
        await SubmitWafSurveyAsync(
            client,
            organizationId,
            "Medium",
            DateTime.UtcNow.Date);

        var detail = await client.GetFromJsonAsync<IssueDetail>(
            $"/api/issues/{issue.Id}");

        Assert.IsNotNull(detail);
        var match = detail.ViolationMatches.Single();
        Assert.AreEqual("Medium", match.SelfReportedState);
        Assert.AreEqual("NoDiscrepancy", match.DiscrepancyStatus);
        Assert.IsFalse(match.IsSelfAssessmentDiscrepancy);
    }

    private static async Task<IssueDetail> CreateIssueAsync(
        HttpClient client,
        int organizationId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/issues",
            new CreateIssueRequest(
                "Checkout outage",
                "Customers could not complete purchases.",
                "A single point of failure existed in the payment tier."));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssueDetail>())!;
    }

    private static async Task SubmitWafSurveyAsync(
        HttpClient client,
        int organizationId,
        string responseLevel,
        DateTime? snapshotDate = null)
    {
        var templates = await client.GetFromJsonAsync<SurveyTemplateSummary[]>(
            "/api/admin/survey-templates");
        Assert.IsNotNull(templates);
        var summary = templates.Single(template =>
            template.Name == "Azure WAF Transformation Pulse");
        var template = await client.GetFromJsonAsync<SurveyTemplateDetail>(
            $"/api/admin/survey-templates/{summary.Id}");
        Assert.IsNotNull(template);

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/survey-submissions",
            new
            {
                surveyTemplateId = template.Id,
                label = "WAF pulse",
                snapshotDate = snapshotDate ?? DateTime.UtcNow.Date,
                answers = template.Questions.Select(question => new
                {
                    surveyQuestionId = question.Id,
                    responseLevel,
                    notes = (string?)null
                })
            });
        response.EnsureSuccessStatusCode();
    }

    private sealed record CreateIssueRequest(
        string Title,
        string Description,
        string? RootCause);

    private sealed record IssueDetail(
        int Id,
        int OrganizationId,
        string Title,
        string Description,
        string RootCause,
        string MatchingStatus,
        string? MatchingError,
        DateTime CreatedAt,
        DateTime? MatchedAt,
        ViolationMatchDetail[] ViolationMatches);

    private sealed record ViolationMatchDetail(
        int Id,
        string ControlCode,
        decimal MatchScore,
        string ReviewStatus,
        bool IsSelfAssessmentDiscrepancy,
        string DiscrepancyStatus,
        string? SelfReportedState);

    private sealed record SurveyTemplateSummary(int Id, string Name);

    private sealed record SurveyTemplateDetail(
        int Id,
        SurveyQuestionDetail[] Questions);

    private sealed record SurveyQuestionDetail(int Id);

    private sealed class StubIssueMatchingService : IIssueMatchingService
    {
        public StubIssueMatchingService(IssueMatchResult? result = null)
        {
            Result = result ?? new IssueMatchResult([], []);
        }

        public IssueMatchResult Result { get; set; }

        public Exception? Failure { get; set; }

        public Task<IssueMatchResult> MatchAsync(
            IssueMatchRequest request,
            CancellationToken cancellationToken = default)
        {
            return Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<IssueMatchResult>(Failure);
        }
    }

    private sealed class IssueApplication(
        string databasePath,
        WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        private readonly string databasePath = databasePath;
        private readonly WebApplicationFactory<Program> factory = factory;

        public static IssueApplication Create(IIssueMatchingService matchingService)
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-issues-{Guid.NewGuid():N}.db");
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
                    services.RemoveAll<IIssueMatchingService>();
                    services.AddSingleton(matchingService);
                }));
            return new IssueApplication(databasePath, factory);
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
