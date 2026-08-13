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
public sealed partial class SurveyUiTests
{
    [TestMethod]
    public async Task Organization_can_complete_a_due_survey_and_it_is_no_longer_due()
    {
        await using var application = SurveyApplication.Create();
        var organizationId = await application.CreateOrganizationAsync("Contoso");
        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var organizationsResponse = await client.GetAsync(
            "/Organizations/Surveys");
        Assert.AreEqual(HttpStatusCode.OK, organizationsResponse.StatusCode);
        var organizationsPage = await organizationsResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(organizationsPage, "Contoso");
        StringAssert.Contains(
            organizationsPage,
            $"href=\"/Organizations/{organizationId}/Surveys\"");

        using var dueResponse = await client.GetAsync(
            $"/Organizations/{organizationId}/Surveys");
        var duePage = await dueResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(
            HttpStatusCode.OK,
            dueResponse.StatusCode,
            duePage);
        StringAssert.Contains(duePage, "Surveys due for Contoso");
        StringAssert.Contains(duePage, "Azure WAF Transformation Pulse");
        StringAssert.Contains(duePage, "Overdue");

        var templateId = await application.GetTemplateIdAsync(
            "Azure WAF Transformation Pulse");
        using var formResponse = await client.GetAsync(
            $"/Organizations/{organizationId}/Surveys/{templateId}");
        Assert.AreEqual(HttpStatusCode.OK, formResponse.StatusCode);
        var formPage = await formResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(formPage, "Self-Reported State");
        StringAssert.Contains(formPage, "Very high");
        StringAssert.Contains(formPage, "Not applicable");
        StringAssert.Contains(formPage, "required");
        StringAssert.Contains(formPage, "Required");
        StringAssert.Contains(formPage, "name=\"Input.Label\"");
        StringAssert.Contains(formPage, "name=\"Input.SnapshotDate\"");

        var token = WebUtility.HtmlDecode(
            AntiforgeryTokenRegex().Match(formPage).Groups["token"].Value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token));
        var questionIds = QuestionIdRegex()
            .Matches(formPage)
            .Select(match => match.Groups["id"].Value)
            .ToArray();
        Assert.HasCount(20, questionIds);
        var formValues = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Input.Label", "2026 Q3 WAF pulse"),
            new("Input.SnapshotDate", DateTime.UtcNow.ToString("yyyy-MM-dd")),
            new("Input.Notes", "Quarterly check-in")
        };
        for (var index = 0; index < questionIds.Length; index++)
        {
            formValues.Add(new(
                $"Input.Answers[{index}].SurveyQuestionId",
                questionIds[index]));
            formValues.Add(new(
                $"Input.Answers[{index}].ResponseLevel",
                "High"));
        }

        using var submitResponse = await client.PostAsync(
            $"/Organizations/{organizationId}/Surveys/{templateId}",
            new FormUrlEncodedContent(formValues));

        var submitPage = await submitResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(
            HttpStatusCode.Redirect,
            submitResponse.StatusCode,
            submitPage);
        Assert.AreEqual(
            $"/Organizations/{organizationId}/Surveys",
            submitResponse.Headers.Location?.OriginalString);
        using var confirmationResponse = await client.GetAsync(
            submitResponse.Headers.Location);
        Assert.AreEqual(HttpStatusCode.OK, confirmationResponse.StatusCode);
        var confirmationPage = await confirmationResponse.Content.ReadAsStringAsync();
        StringAssert.Contains(
            confirmationPage,
            "Azure WAF Transformation Pulse was submitted successfully.");
        Assert.DoesNotContain(
            "href=\"/Organizations/" + organizationId + "/Surveys/" + templateId + "\"",
            confirmationPage);

        using var completedFormResponse = await client.GetAsync(
            $"/Organizations/{organizationId}/Surveys/{templateId}");
        Assert.AreEqual(HttpStatusCode.NotFound, completedFormResponse.StatusCode);
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTokenRegex();

    [GeneratedRegex(
        "name=\"Input\\.Answers\\[\\d+\\]\\.SurveyQuestionId\"[^>]*value=\"(?<id>\\d+)\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex QuestionIdRegex();

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
                $"bpradar-survey-ui-{Guid.NewGuid():N}.db");
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

        public HttpClient CreateClient(WebApplicationFactoryClientOptions options) =>
            factory.CreateClient(options);

        public async Task<int> CreateOrganizationAsync(string name)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            var organization = new Organization { Name = name };
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync();
            return organization.Id;
        }

        public async Task<int> GetTemplateIdAsync(string name)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
            return await dbContext.SurveyTemplates
                .Where(template => template.Name == name)
                .Select(template => template.Id)
                .SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await factory.DisposeAsync();
            File.Delete(databasePath);
        }
    }
}
