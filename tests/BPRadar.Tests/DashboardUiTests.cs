using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.Surveys;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests;

[TestClass]
[DoNotParallelize]
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
        StringAssert.Contains(page, "Export CSV");
        StringAssert.Contains(page, "Print / Save as PDF");
        StringAssert.Contains(page, "handler=Csv");
        StringAssert.Contains(page, "/Dashboard/Report?");
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

    [TestMethod]
    public async Task Csv_export_contains_all_requested_sections_and_trace_metadata()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Dashboard?handler=Csv" +
            $"&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&baselineProfileId={setup.ProfileId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&includeSurveyDomainDeltas=true");
        request.Headers.Add("X-Correlation-ID", "export-correlation-23");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "Report Metadata");
        StringAssert.Contains(csv, "Organization,Contoso");
        StringAssert.Contains(csv, "Exported UTC,2026-08-13T08:00:00.0000000Z");
        StringAssert.Contains(csv, "Correlation ID,export-correlation-23");
        StringAssert.Contains(csv, "Framework Summary");
        StringAssert.Contains(
            csv,
            "Framework,Version,Assessment,Completion %,Compliance %,Target %,Delta %,Gap Count");
        StringAssert.Contains(
            csv,
            "Test Framework,1.0,Current review,75,33.33,80,-46.67,2");
        StringAssert.Contains(csv, "Gap List");
        StringAssert.Contains(
            csv,
            "Framework,Domain,Control Code,Title,Status,Score,Notes");
        StringAssert.Contains(
            csv,
            "Test Framework,TEST - Test Domain,TEST-2,Test control 2,Partial,10,Note 2");
        StringAssert.Contains(
            csv,
            "Test Framework,TEST - Test Domain,TEST-3,Test control 3,Non-Compliant,20,Note 3");
        StringAssert.Contains(csv, "Survey Submission History");
        StringAssert.Contains(
            csv,
            "Snapshot Date,Submission,Profile Score,Delta,Key Notes");
        StringAssert.Contains(csv, "2026-07-01,Q3 pulse,75,25,Latest snapshot");
        StringAssert.Contains(csv, "2026-04-01,Q2 pulse,50,,Previous snapshot");
        StringAssert.Contains(csv, "Survey Domain Deltas");
        StringAssert.Contains(csv, "Domain,Previous Score,Latest Score,Delta");
        StringAssert.Contains(csv, "TEST - Test Domain,50,75,25");
    }

    [TestMethod]
    public async Task Csv_export_respects_active_gap_filters()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv" +
            $"&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&frameworkId={setup.TargetedFrameworkId}" +
            $"&domainId={setup.DomainId}" +
            "&gapStatus=Partial");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "TEST-2");
        Assert.IsFalse(csv.Contains("TEST-3", StringComparison.Ordinal));
        Assert.IsFalse(csv.Contains("Untargeted Framework", StringComparison.Ordinal));
        Assert.IsFalse(csv.Contains("Survey Domain Deltas", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Csv_export_escapes_free_form_audit_evidence()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(
            partialNotes: "Needs, \"audit\"\r\nfollow-up");

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv" +
            $"&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "\"Needs, \"\"audit\"\"\r\nfollow-up\"");
    }

    [TestMethod]
    public async Task Csv_export_neutralizes_formula_like_audit_evidence()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(partialNotes: "=1+1");

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv" +
            $"&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(
            csv,
            "TEST-2,Test control 2,Partial,10,'=1+1");
    }

    [TestMethod]
    public async Task Print_report_renders_audit_handoff_content_and_gap_continuation()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(additionalGapCount: 55);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Dashboard/Report?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&baselineProfileId={setup.ProfileId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}");
        request.Headers.Add("X-Correlation-ID", "report-correlation-23");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var report = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Contains(report, "Audit handoff report");
        StringAssert.Contains(report, "Contoso");
        StringAssert.Contains(report, "Test Framework 1.0 - Current review");
        StringAssert.Contains(report, "Generated 2026-08-13 08:00:00 UTC");
        StringAssert.Contains(report, "Correlation ID: report-correlation-23");
        StringAssert.Contains(report, "Framework summary");
        StringAssert.Contains(report, "id=\"report-radar-chart\"");
        StringAssert.Contains(report, "class=\"radar-series target\"");
        StringAssert.Contains(report, "Survey transformation summary");
        StringAssert.Contains(report, "75% profile score");
        StringAssert.Contains(report, "+25 points vs previous");
        StringAssert.Contains(report, "id=\"report-survey-trend\"");
        StringAssert.Contains(report, "Gap details");
        StringAssert.Contains(report, "EXTRA-001");
        StringAssert.Contains(report, "7 additional gaps are available in the CSV export.");
        StringAssert.Contains(report, "window.print()");
        StringAssert.Contains(report, "@media print");
    }

    [TestMethod]
    public async Task Export_routes_apply_identical_scope_and_explicit_no_baseline_semantics()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(additionalGapCount: 1);
        var scope =
            $"organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            "&baselineProfileId=" +
            $"&frameworkId={setup.TargetedFrameworkId}" +
            $"&domainId={setup.DomainId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&gapStatus=NonCompliant&sort=Title&sortDescending=true";

        using var csvResponse = await client.GetAsync($"/Dashboard?handler=Csv&{scope}");
        using var reportResponse = await client.GetAsync($"/Dashboard/Report?{scope}");

        Assert.AreEqual(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, reportResponse.StatusCode);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        var report = WebUtility.HtmlDecode(
            await reportResponse.Content.ReadAsStringAsync());
        Assert.IsLessThan(
            csv.IndexOf("EXTRA-001", StringComparison.Ordinal),
            csv.IndexOf("TEST-3", StringComparison.Ordinal));
        Assert.IsLessThan(
            report.IndexOf("EXTRA-001", StringComparison.Ordinal),
            report.IndexOf("TEST-3", StringComparison.Ordinal));
        Assert.IsFalse(csv.Contains("TEST-2", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("TEST-2", StringComparison.Ordinal));
        StringAssert.Contains(csv, "Q3 pulse");
        StringAssert.Contains(report, "Transformation pulse");
        Assert.IsFalse(csv.Contains(",80,-46.67,", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains(">Target<", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Export_routes_return_correlation_bearing_problem_responses()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        foreach (var (path, expectedStatus) in new[]
                 {
                     (
                         "/Dashboard?handler=Csv&organizationId=999999",
                         HttpStatusCode.NotFound),
                     (
                         "/Dashboard/Report?organizationId=999999",
                         HttpStatusCode.NotFound),
                     (
                         "/Dashboard?handler=Csv",
                         HttpStatusCode.NotFound),
                     (
                         "/Dashboard/Report",
                         HttpStatusCode.NotFound),
                     (
                         $"/Dashboard?handler=Csv&organizationId={setup.OrganizationId}&sort=invalid",
                         HttpStatusCode.BadRequest),
                     (
                         $"/Dashboard/Report?organizationId={setup.OrganizationId}&sort=invalid",
                         HttpStatusCode.BadRequest)
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Correlation-ID", "missing-export-scope-32");
            using var response = await client.SendAsync(request);

            Assert.AreEqual(expectedStatus, response.StatusCode);
            Assert.AreEqual(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            Assert.AreEqual(
                "missing-export-scope-32",
                problem.RootElement.GetProperty("correlationId").GetString());
        }
    }

    [TestMethod]
    public async Task Export_completion_traces_include_business_ids_without_notes()
    {
        await using var application = DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(
            partialNotes: "secret diagnostic notes");
        using var trace = new TraceCapture();
        var scope =
            $"organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&frameworkId={setup.TargetedFrameworkId}" +
            $"&domainId={setup.DomainId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}";

        using var csvResponse = await client.GetAsync($"/Dashboard?handler=Csv&{scope}");
        using var reportResponse = await client.GetAsync($"/Dashboard/Report?{scope}");

        Assert.AreEqual(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, reportResponse.StatusCode);
        var output = trace.Output;
        StringAssert.Contains(output, "operation=CsvExportCompleted");
        StringAssert.Contains(output, "operation=PrintReportCompleted");
        StringAssert.Contains(output, $"OrganizationId={setup.OrganizationId}");
        StringAssert.Contains(output, $"AssessmentIds={setup.AssessmentId}");
        StringAssert.Contains(output, $"FrameworkIds={setup.TargetedFrameworkId}");
        StringAssert.Contains(output, $"DomainId={setup.DomainId}");
        StringAssert.Contains(output, $"SurveyTemplateId={setup.SurveyTemplateId}");
        Assert.IsFalse(
            output.Contains("secret diagnostic notes", StringComparison.Ordinal));
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
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Tracing:Level", "All");
                    builder.ConfigureServices(services =>
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
                    });
                });
            return new DashboardApplication(databasePath, factory);
        }

        public HttpClient CreateClient() => factory.CreateClient();

        public HttpClient CreateClient(WebApplicationFactoryClientOptions options) =>
            factory.CreateClient(options);

        public async Task<DashboardSetup> SeedAsync(
            int additionalGapCount = 0,
            string partialNotes = "Note 2")
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
                    Notes = statuses[index] == ComplianceStatus.Partial
                        ? partialNotes
                        : $"Note {index + 1}",
                    Source = ResultSource.Manual,
                    UpdatedAt = now
                });
                if (statuses[index] == ComplianceStatus.Partial)
                {
                    partialControl = control;
                }
            }

            for (var index = 0; index < additionalGapCount; index++)
            {
                var control = new Control
                {
                    Code = $"EXTRA-{index + 1:000}",
                    Title = $"Additional gap {index + 1}",
                    Description = $"Additional description {index + 1}",
                    SortOrder = statuses.Length + index + 1
                };
                domain.Controls.Add(control);
                assessment.Results.Add(new AssessmentResult
                {
                    Control = control,
                    Status = ComplianceStatus.NonCompliant,
                    Score = 0m,
                    Notes = $"Additional note {index + 1}",
                    Source = ResultSource.Manual,
                    UpdatedAt = now
                });
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
                domain.Id,
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
        int DomainId,
        int UntargetedAssessmentId,
        int UntargetedFrameworkId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TraceCapture : IDisposable
    {
        private readonly StringBuilder output = new();
        private readonly TextWriterTraceListener listener;

        public TraceCapture()
        {
            listener = new TextWriterTraceListener(new StringWriter(output));
            BPRadarTrace.Source.Listeners.Add(listener);
        }

        public string Output
        {
            get
            {
                listener.Flush();
                return output.ToString();
            }
        }

        public void Dispose()
        {
            BPRadarTrace.Source.Listeners.Remove(listener);
            listener.Dispose();
        }
    }
}
