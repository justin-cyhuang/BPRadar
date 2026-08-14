using System.Net;

namespace BPRadar.Tests;

public sealed partial class DashboardPrintReportTests
{
    [TestMethod]
    public async Task Print_report_records_when_no_survey_template_is_selected()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard/Report?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}&surveyTemplateId=");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var report = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Contains(
            report,
            "No Survey Template selected. Survey evidence was excluded from this report.");
    }

    [TestMethod]
    public async Task Print_report_records_when_selected_survey_template_has_no_submissions()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync(includeSurveySubmissions: false);

        using var response = await client.GetAsync(
            $"/Dashboard/Report?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var report = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Contains(report, "Transformation pulse");
        StringAssert.Contains(
            report,
            "No submissions are available for the selected Survey Template.");
        Assert.IsFalse(
            report.Contains("id=\"report-survey-trend\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Print_report_retains_scored_survey_history()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard/Report?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var report = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Contains(
            report,
            "Scored submission history is available for the selected Survey Template.");
        StringAssert.Contains(report, "Latest Self-Reported State");
        StringAssert.Contains(report, "75% survey score");
        StringAssert.Contains(report, "id=\"report-survey-trend\"");
    }

    [TestMethod]
    public async Task Print_report_filters_and_records_the_survey_submission_date_range()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard/Report?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&surveySubmissionFrom=2026-07-01" +
            "&surveySubmissionTo=2026-07-01");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var report = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        StringAssert.Contains(report, "Survey submission date range:");
        StringAssert.Contains(report, "2026-07-01");
        StringAssert.Contains(report, "75% survey score");
        Assert.IsFalse(report.Contains("2026-04-01", StringComparison.Ordinal));
        Assert.IsFalse(
            report.Contains("+25 points vs previous survey", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Print_report_rejects_an_inverted_survey_submission_date_range()
    {
        await using var application = DashboardUiTests.DashboardApplication.Create();
        using var client = application.CreateClient();
        var setup = await application.SeedAsync();

        using var response = await client.GetAsync(
            $"/Dashboard/Report?organizationId={setup.OrganizationId}" +
            $"&assessmentIds={setup.AssessmentId}" +
            $"&surveyTemplateId={setup.SurveyTemplateId}" +
            "&surveySubmissionFrom=2026-08-01" +
            "&surveySubmissionTo=2026-07-01");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
