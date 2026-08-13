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
        Assert.IsFalse(page.Contains("<th>Score</th>", StringComparison.Ordinal));
        Assert.IsFalse(page.Contains("value=\"Score\"", StringComparison.Ordinal));
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
        Assert.IsFalse(checklist.Contains(
            "aria-label=\"Score for",
            StringComparison.Ordinal));
        Assert.IsFalse(checklist.Contains("type=\"number\"", StringComparison.Ordinal));
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
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
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
            dbContext.AddRange(assessment, profile);
            await dbContext.SaveChangesAsync();
            return new DashboardSetup(
                organization.Id,
                assessment.Id,
                profile.Id,
                partialControl!.Id);
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
        int PartialControlId);
}
