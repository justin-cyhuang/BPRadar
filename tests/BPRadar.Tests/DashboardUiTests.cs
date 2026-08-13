using System.Net;
using System.Text.RegularExpressions;
using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests;

[TestClass]
public sealed class DashboardUiTests
{
    [TestMethod]
    public async Task Root_redirects_to_dashboard()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/Dashboard", response.Headers.Location?.OriginalString);
    }

    [TestMethod]
    public async Task Dashboard_renders_default_scope_metrics_filters_and_gap_deep_links()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?organizationId={setup.OrganizationId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(page, "Dashboard");
        StringAssert.Contains(page, "name=\"AssessmentIds\"");
        StringAssert.Matches(
            page,
            new Regex(
                $"value=\"{setup.AssessmentId}\"\\s+checked=\"checked\"",
                RegexOptions.CultureInvariant));
        StringAssert.Matches(
            page,
            new Regex(
                $"value=\"{setup.ProfileId}\"\\s+selected=\"selected\"",
                RegexOptions.CultureInvariant));
        StringAssert.Contains(page, "75% completion");
        StringAssert.Contains(page, "33.3% compliance");
        StringAssert.Contains(page, "2 gaps");
        StringAssert.Contains(page, "80% target");
        StringAssert.Contains(page, "-46.7% vs target");
        StringAssert.Contains(page, "name=\"FrameworkId\"");
        StringAssert.Contains(page, "name=\"DomainId\"");
        StringAssert.Contains(page, "name=\"GapStatus\"");
        StringAssert.Contains(page, "TEST-2");
        StringAssert.Contains(page, "TEST-3");
        StringAssert.Contains(
            page,
            $"/Assessments/{setup.AssessmentId}#control-{setup.PartialControlId}");
        StringAssert.Contains(
            page,
            $"data-href=\"/Assessments/{setup.AssessmentId}#control-{setup.PartialControlId}\"");

        using var checklistResponse = await client.GetAsync(
            $"/Assessments/{setup.AssessmentId}");
        Assert.AreEqual(HttpStatusCode.OK, checklistResponse.StatusCode);
        var checklist = await checklistResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(
            checklist,
            $"id=\"control-{setup.PartialControlId}\"");
        StringAssert.Contains(checklist, "window.location.hash");
    }

    [TestMethod]
    public async Task Dashboard_renders_radar_target_and_selected_survey_tracking_panel()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&baselineProfileId={setup.ProfileId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Contains(page, "name=\"SurveyTemplateId\"");
        StringAssert.Matches(
            page,
            new Regex(
                $"value=\"{setup.SurveyTemplateId}\"\\s+selected=\"selected\"",
                RegexOptions.CultureInvariant));
        StringAssert.Contains(page, "id=\"radar-chart\"");
        foreach (var level in new[] { 25, 50, 75, 100 })
        {
            StringAssert.Contains(page, $"data-grid-level=\"{level}\"");
        }

        StringAssert.Contains(
            page,
            $"data-radar-series-assessment-id=\"{setup.AssessmentId}\"");
        StringAssert.Contains(page, "class=\"radar-series target\"");
        StringAssert.Contains(page, ">Target<");
        StringAssert.Contains(page, "Transformation pulse");
        StringAssert.Contains(page, "75% profile score");
        StringAssert.Contains(page, "+25 points vs previous");
        StringAssert.Contains(page, "On time");
        StringAssert.Contains(page, "Q3 pulse");
        StringAssert.Contains(page, "Q2 pulse");
        StringAssert.Contains(page, "id=\"survey-trend\"");
        StringAssert.Contains(page, "data-score=\"50\"");
        StringAssert.Contains(page, "data-score=\"75\"");
    }

    [TestMethod]
    public async Task Dashboard_renders_only_configured_partial_target_markers()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&assessmentIds={setup.UntargetedAssessmentId}" +
            $"&baselineProfileId={setup.ProfileId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(page, "class=\"radar-target-point\"");
        StringAssert.Contains(
            page,
            $"data-target-framework-id=\"{setup.TargetedFrameworkId}\"");
        Assert.IsFalse(
            page.Contains(
                $"data-target-framework-id=\"{setup.UntargetedFrameworkId}\"",
                StringComparison.Ordinal));
        Assert.IsFalse(
            page.Contains(
                "class=\"radar-series target\"",
                StringComparison.Ordinal));
        StringAssert.Contains(
            page,
            "Target markers appear only on frameworks with a configured target.");
    }

    private sealed class DashboardApplication(
        string databasePath,
        WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        private readonly string databasePath = databasePath;
        private readonly WebApplicationFactory<Program> factory = factory;

        public static DashboardApplication Create()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-dashboard-ui-{Guid.NewGuid():N}.db");
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                    services.RemoveAll<TimeProvider>();
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
                    services.AddSingleton<TimeProvider>(
                        new FixedTimeProvider(
                            new DateTimeOffset(
                                2026,
                                8,
                                13,
                                8,
                                0,
                                0,
                                TimeSpan.Zero)));
                }));
            return new DashboardApplication(databasePath, factory);
        }

        public HttpClient CreateClient() => factory.CreateClient();

        public HttpClient CreateClient(WebApplicationFactoryClientOptions options) =>
            factory.CreateClient(options);

        public async Task<DashboardSetup> SeedAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            var organization = new Organization { Name = "Contoso" };
            var framework = new Framework
            {
                Name = "Test Framework",
                Version = "1.0",
                Description = "Dashboard UI framework"
            };
            var domain = new Domain
            {
                Code = "TEST",
                Name = "Test Domain",
                SortOrder = 1
            };
            framework.Domains.Add(domain);
            var statuses = new[]
            {
                ComplianceStatus.Compliant,
                ComplianceStatus.Partial,
                ComplianceStatus.NonCompliant,
                ComplianceStatus.NotAssessed
            };
            var now = new DateTime(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc);
            var assessment = new Assessment
            {
                Organization = organization,
                Framework = framework,
                Label = "Current review",
                SnapshotDate = now.Date,
                CreatedAt = now,
                UpdatedAt = now
            };
            Control? partialControl = null;
            for (var index = 0; index < statuses.Length; index++)
            {
                var control = new Control
                {
                    Code = $"TEST-{index + 1}",
                    Title = $"Test control {index + 1}",
                    Description = $"Description {index + 1}",
                    SortOrder = index + 1
                };
                domain.Controls.Add(control);
                assessment.Results.Add(new AssessmentResult
                {
                    Control = control,
                    Status = statuses[index],
                    Score = index * 10m,
                    Notes = $"Note {index + 1}",
                    Source = ResultSource.Manual,
                    UpdatedAt = now
                });
                if (statuses[index] == ComplianceStatus.Partial)
                {
                    partialControl = control;
                }
            }

            var profile = new BaselineProfile
            {
                Organization = organization,
                Name = "Internal target",
                IsDefault = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            profile.Targets.Add(new BaselineTarget
            {
                Framework = framework,
                TargetCompliancePercent = 80m
            });
            var untargetedFramework = new Framework
            {
                Name = "Untargeted Framework",
                Version = "1.0",
                Description = "Dashboard framework without a baseline target"
            };
            var untargetedAssessment = new Assessment
            {
                Organization = organization,
                Framework = untargetedFramework,
                Label = "Untargeted review",
                SnapshotDate = now.Date,
                CreatedAt = now,
                UpdatedAt = now
            };
            var surveyTemplate = new SurveyTemplate
            {
                Name = "Transformation pulse",
                Cadence = SurveyCadence.Quarterly,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            var surveyQuestion = new SurveyQuestion
            {
                Code = "PULSE-1",
                Prompt = "How mature is this capability?",
                Domain = domain,
                Weight = 1m,
                SortOrder = 1,
                IsRequired = true
            };
            surveyTemplate.Questions.Add(surveyQuestion);
            var previousSubmission = new SurveySubmission
            {
                Organization = organization,
                SurveyTemplate = surveyTemplate,
                Label = "Q2 pulse",
                SnapshotDate = new DateTime(2026, 4, 1),
                SubmittedAt = new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc),
                Notes = "Previous snapshot"
            };
            previousSubmission.Responses.Add(new SurveyResponse
            {
                SurveyQuestion = surveyQuestion,
                ResponseLevel = SurveyResponseLevel.Medium
            });
            var latestSubmission = new SurveySubmission
            {
                Organization = organization,
                SurveyTemplate = surveyTemplate,
                Label = "Q3 pulse",
                SnapshotDate = new DateTime(2026, 7, 1),
                SubmittedAt = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc),
                Notes = "Latest snapshot"
            };
            latestSubmission.Responses.Add(new SurveyResponse
            {
                SurveyQuestion = surveyQuestion,
                ResponseLevel = SurveyResponseLevel.High
            });
            dbContext.AddRange(
                assessment,
                untargetedAssessment,
                profile,
                previousSubmission,
                latestSubmission);
            await dbContext.SaveChangesAsync();
            return new DashboardSetup(
                organization.Id,
                assessment.Id,
                profile.Id,
                partialControl!.Id,
                surveyTemplate.Id,
                framework.Id,
                untargetedAssessment.Id,
                untargetedFramework.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await factory.DisposeAsync();
            File.Delete(databasePath);
        }
    }

    private sealed record DashboardSetup(
        int OrganizationId,
        int AssessmentId,
        int ProfileId,
        int PartialControlId,
        int SurveyTemplateId,
        int TargetedFrameworkId,
        int UntargetedAssessmentId,
        int UntargetedFrameworkId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
