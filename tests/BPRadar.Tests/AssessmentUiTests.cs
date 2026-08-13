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
public sealed partial class AssessmentUiTests
{
    [TestMethod]
    public async Task Assessor_can_create_an_assessment_and_see_its_completion()
    {
        await using var application = AssessmentApplication.Create();
        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var frameworkId = await application.CreateFrameworkAsync(controlCount: 4);

        using var createResponse = await client.GetAsync("/Assessments/Create");
        Assert.AreEqual(HttpStatusCode.OK, createResponse.StatusCode);
        var createPage = await createResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(createPage, "New organization name");
        StringAssert.Contains(createPage, "Test Framework 1.0");
        StringAssert.Contains(createPage, "name=\"Input.Label\"");
        StringAssert.Contains(createPage, "name=\"Input.SnapshotDate\"");
        var token = WebUtility.HtmlDecode(
            AntiforgeryTokenRegex().Match(createPage).Groups["token"].Value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token));

        using var submitResponse = await client.PostAsync(
            "/Assessments/Create",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("Input.NewOrganizationName", "Contoso"),
                new("Input.FrameworkId", frameworkId.ToString()),
                new("Input.Label", "2026 Q1 Security Review"),
                new("Input.SnapshotDate", DateTime.UtcNow.ToString("yyyy-MM-dd"))
            ]));

        Assert.AreEqual(HttpStatusCode.Redirect, submitResponse.StatusCode);
        Assert.AreEqual(
            "/Assessments",
            submitResponse.Headers.Location?.OriginalString);
        await application.MarkOneResultCompliantAsync();

        using var listResponse = await client.GetAsync("/Assessments");
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);
        var listPage = await listResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(listPage, "Contoso");
        StringAssert.Contains(listPage, "Test Framework 1.0");
        StringAssert.Contains(listPage, "2026 Q1 Security Review");
        StringAssert.Contains(listPage, "25%");

        var assessment = await application.GetAssessmentAsync();
        Assert.AreEqual("Contoso", assessment.Organization.Name);
        Assert.HasCount(4, assessment.Results);
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTokenRegex();

    private sealed class AssessmentApplication(
        string databasePath,
        WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        private readonly string databasePath = databasePath;
        private readonly WebApplicationFactory<Program> factory = factory;

        public static AssessmentApplication Create()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-assessment-ui-{Guid.NewGuid():N}.db");
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
                }));
            return new AssessmentApplication(databasePath, factory);
        }

        public HttpClient CreateClient(WebApplicationFactoryClientOptions options) =>
            factory.CreateClient(options);

        public async Task<int> CreateFrameworkAsync(int controlCount)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            var framework = new Framework
            {
                Name = "Test Framework",
                Version = "1.0",
                Description = "Assessment UI test framework"
            };
            var domain = new Domain
            {
                Code = "TEST",
                Name = "Test Domain",
                SortOrder = 1
            };
            framework.Domains.Add(domain);
            for (var index = 1; index <= controlCount; index++)
            {
                domain.Controls.Add(new Control
                {
                    Code = $"TEST-{index}",
                    Title = $"Test control {index}",
                    Description = $"Test control {index}",
                    SortOrder = index
                });
            }

            dbContext.Frameworks.Add(framework);
            await dbContext.SaveChangesAsync();
            return framework.Id;
        }

        public async Task MarkOneResultCompliantAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            var result = await dbContext.AssessmentResults.FirstAsync();
            result.Status = ComplianceStatus.Compliant;
            result.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        public async Task<Assessment> GetAssessmentAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            return await dbContext.Assessments
                .AsNoTracking()
                .Include(item => item.Organization)
                .Include(item => item.Results)
                .SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await factory.DisposeAsync();
            File.Delete(databasePath);
        }
    }
}
