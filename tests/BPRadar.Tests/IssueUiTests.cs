using System.Net;
using System.Text.RegularExpressions;
using BPRadar.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests;

[TestClass]
public sealed partial class IssueUiTests
{
    [TestMethod]
    public async Task Organization_can_view_violation_matches_with_discrepancies_first()
    {
        await using var application = IssueUiApplication.Create();
        var organizationId = await application.SeedMatchedIssueAsync();
        using var client = application.CreateClient();

        using var response = await client.GetAsync(
            $"/Organizations/{organizationId}/Issues");
        var html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, html);
        StringAssert.Contains(html, "Issues for Contoso");
        StringAssert.Contains(html, "Checkout outage");
        StringAssert.Contains(html, "<summary><span class=\"status\">Matched</span>");
        StringAssert.Contains(html, "Matched");
        StringAssert.Contains(html, "Pending investigation");
        StringAssert.Contains(html, "Pending");
        StringAssert.Contains(html, "Provider unavailable");
        StringAssert.Contains(html, "Failed");
        StringAssert.Contains(html, "RE:05");
        StringAssert.Contains(html, "Self-Assessment Discrepancy");
        StringAssert.Contains(html, "Self-Reported State: High");
        StringAssert.Contains(html, "Matched keywords");
        StringAssert.Contains(html, "Confirm");
        StringAssert.Contains(html, "Dismiss");
        Assert.IsLessThan(
            html.IndexOf("A.8.13", StringComparison.Ordinal),
            html.IndexOf("RE:05", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Confirmed", "confirmed")]
    [DataRow("Dismissed", "dismissed")]
    public async Task User_can_select_an_organization_and_review_a_candidate(
        string reviewStatus,
        string confirmationVerb)
    {
        await using var application = IssueUiApplication.Create();
        var organizationId = await application.SeedMatchedIssueAsync();
        var violationMatchId = await application.GetOpenMatchIdAsync("RE:05");
        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var organizationsHtml = await client.GetStringAsync("/Organizations/Issues");
        StringAssert.Contains(
            organizationsHtml,
            $"href=\"/Organizations/{organizationId}/Issues\"");
        using var pageResponse = await client.GetAsync(
            $"/Organizations/{organizationId}/Issues");
        var pageHtml = await pageResponse.Content.ReadAsStringAsync();
        var token = WebUtility.HtmlDecode(
            AntiforgeryTokenRegex().Match(pageHtml).Groups["token"].Value);

        using var reviewResponse = await client.PostAsync(
            $"/Organizations/{organizationId}/Issues?handler=Review",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("violationMatchId", violationMatchId.ToString()),
                new("reviewStatus", reviewStatus)
            ]));

        Assert.AreEqual(HttpStatusCode.Redirect, reviewResponse.StatusCode);
        Assert.AreEqual(
            $"/Organizations/{organizationId}/Issues",
            reviewResponse.Headers.Location?.OriginalString);
        var confirmedHtml = await client.GetStringAsync(
            reviewResponse.Headers.Location);
        StringAssert.Contains(
            confirmedHtml,
            $"Violation Match {confirmationVerb}.");
        StringAssert.Contains(confirmedHtml, "RE:05");
        StringAssert.Contains(confirmedHtml, reviewStatus);
        if (reviewStatus == "Dismissed")
        {
            Assert.IsFalse(DismissedMatchOpenRegex().IsMatch(confirmedHtml));
        }
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTokenRegex();

    [GeneratedRegex(
        "<details[^>]*class=\"[^\"]*dismissed[^\"]*\"[^>]*\\sopen(?:=|[\\s>])",
        RegexOptions.IgnoreCase)]
    private static partial Regex DismissedMatchOpenRegex();

    private sealed class IssueUiApplication(
        string databasePath,
        WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        private readonly string databasePath = databasePath;
        private readonly WebApplicationFactory<Program> factory = factory;

        public static IssueUiApplication Create()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-issue-ui-{Guid.NewGuid():N}.db");
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
                }));
            return new IssueUiApplication(databasePath, factory);
        }

        public HttpClient CreateClient(
            WebApplicationFactoryClientOptions? options = null) =>
            options is null
                ? factory.CreateClient()
                : factory.CreateClient(options);

        public async Task<int> GetOpenMatchIdAsync(string controlCode)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            return await dbContext.ViolationMatches
                .Where(match =>
                    match.Control.Code == controlCode &&
                    match.ReviewStatus == ViolationMatchReviewStatus.Open)
                .Select(match => match.Id)
                .SingleAsync();
        }

        public async Task<int> SeedMatchedIssueAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            var controls = await dbContext.Controls
                .Where(control => control.Code == "RE:05" || control.Code == "A.8.13")
                .ToDictionaryAsync(control => control.Code);
            var organization = new Organization { Name = "Contoso" };
            var issue = new Issue
            {
                Organization = organization,
                Title = "Checkout outage",
                Description = "Customers could not complete purchases.",
                RootCause = "A single point of failure caused data loss.",
                MatchingStatus = IssueMatchingStatus.Matched,
                CreatedAt = DateTime.UtcNow,
                MatchedAt = DateTime.UtcNow
            };
            issue.ViolationMatches.Add(new ViolationMatch
            {
                Control = controls["RE:05"],
                MatchedKeywords = """["missing redundancy"]""",
                MatchScore = 0.80m,
                ReviewStatus = ViolationMatchReviewStatus.Open,
                CreatedAt = DateTime.UtcNow
            });
            issue.ViolationMatches.Add(new ViolationMatch
            {
                Control = controls["A.8.13"],
                MatchedKeywords = """["backup failure"]""",
                MatchScore = 0.99m,
                ReviewStatus = ViolationMatchReviewStatus.Open,
                CreatedAt = DateTime.UtcNow
            });
            dbContext.Issues.Add(issue);
            dbContext.Issues.AddRange(
                new Issue
                {
                    Organization = organization,
                    Title = "Pending investigation",
                    Description = "Root Cause is still being documented.",
                    RootCause = string.Empty,
                    MatchingStatus = IssueMatchingStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new Issue
                {
                    Organization = organization,
                    Title = "Failed matching attempt",
                    Description = "The provider could not process this Issue.",
                    RootCause = "A network dependency was unavailable.",
                    MatchingStatus = IssueMatchingStatus.Failed,
                    MatchingError = "Provider unavailable",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    MatchedAt = DateTime.UtcNow.AddMinutes(-1)
                });
            var question = await dbContext.SurveyQuestions
                .Include(item => item.SurveyTemplate)
                .SingleAsync(item => item.ControlId == controls["RE:05"].Id);
            var submission = new SurveySubmission
            {
                Organization = organization,
                SurveyTemplate = question.SurveyTemplate,
                Label = "Current WAF pulse",
                SnapshotDate = DateTime.UtcNow.Date,
                SubmittedAt = DateTime.UtcNow
            };
            submission.Responses.Add(new SurveyResponse
            {
                SurveyQuestion = question,
                ResponseLevel = SurveyResponseLevel.High
            });
            dbContext.SurveySubmissions.Add(submission);
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
