using System.Net;
using System.Text.RegularExpressions;

namespace BPRadar.Tests;

public sealed partial class DashboardCsvExportTests
{
    [TestMethod]
    public async Task Csv_export_records_when_no_survey_template_is_selected()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}&surveyTemplateId=");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "Survey Template,Not selected");
        StringAssert.Contains(csv, "Survey Evidence State,No Survey Template selected");
    }

    [TestMethod]
    public async Task Csv_export_records_when_selected_survey_template_has_no_submissions()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(includeSurveySubmissions: false);

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "Survey Template,Transformation pulse");
        StringAssert.Contains(
            csv,
            "Survey Evidence State,Selected Survey Template has no submissions");
    }

    [TestMethod]
    public async Task Csv_export_records_scored_survey_history()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "Survey Template,Transformation pulse");
        StringAssert.Contains(
            csv,
            "Survey Evidence State,Scored submission history available");
        StringAssert.Contains(csv, "2026-07-01,Q3 pulse,75,25,Latest snapshot");
    }

    [TestMethod]
    public async Task Csv_export_filters_and_records_the_survey_submission_date_range()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&surveySubmissionFrom=2026-07-01" +
            "&surveySubmissionTo=2026-07-01");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(csv, "Survey Submissions From,2026-07-01");
        StringAssert.Contains(csv, "Survey Submissions To,2026-07-01");
        StringAssert.Contains(csv, "2026-07-01,Q3 pulse,75,,Latest snapshot");
        Assert.IsFalse(csv.Contains("Q2 pulse", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Csv_export_rejects_an_inverted_survey_submission_date_range()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?handler=Csv&organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&surveySubmissionFrom=2026-08-01" +
            "&surveySubmissionTo=2026-07-01");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Dashboard_export_controls_preserve_current_survey_template_scope()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&surveySubmissionFrom=2026-04-01" +
            "&surveySubmissionTo=2026-07-01");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Matches(
            page,
            new Regex(
                $"id=\"csv-export\"[\\s\\S]*?name=\"SurveyTemplateId\"\\s+" +
                $"value=\"{setup.SurveyTemplateId}\"",
                RegexOptions.CultureInvariant));
        StringAssert.Matches(
            page,
            new Regex(
                "id=\"csv-export\"[\\s\\S]*?name=\"SurveySubmissionFrom\"\\s+" +
                "value=\"2026-04-01\"",
                RegexOptions.CultureInvariant));
        StringAssert.Matches(
            page,
            new Regex(
                "id=\"csv-export\"[\\s\\S]*?name=\"SurveySubmissionTo\"\\s+" +
                "value=\"2026-07-01\"",
                RegexOptions.CultureInvariant));
        StringAssert.Contains(
            page,
            $"/Dashboard/Report?OrganizationId={setup.OrganizationId}" +
            $"&BaselineProfileId={setup.ProfileId}" +
            $"&SurveyTemplateId={setup.SurveyTemplateId}" +
            "&SurveySubmissionFrom=2026-04-01" +
            "&SurveySubmissionTo=2026-07-01");
    }
}
