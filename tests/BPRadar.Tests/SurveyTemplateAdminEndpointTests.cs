using System.Net;
using BPRadar.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests;

[TestClass]
public sealed class SurveyTemplateAdminEndpointTests
{
    [TestMethod]
    public async Task Admin_listing_shows_seeded_templates_with_expected_question_counts()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"bpradar-web-{Guid.NewGuid():N}.db");

        try
        {
            await using var application = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                    services.AddDbContext<BPRadarDbContext>(
                        options => options.UseSqlite(
                            $"Data Source={databasePath};Pooling=False"));
                }));
            using var client = application.CreateClient();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/Admin/SurveyTemplates");
            request.Headers.Add("X-Correlation-ID", "survey-admin-test");
            var response = await client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(
                "survey-admin-test",
                response.Headers.GetValues("X-Correlation-ID").Single());
            var content = await response.Content.ReadAsStringAsync();
            StringAssert.Contains(content, "Azure WAF Transformation Pulse");
            StringAssert.Contains(content, "ISO 27001 ISMS Transformation Pulse");
            StringAssert.Contains(content, "ISO 20000-1 SMS Transformation Pulse");
            StringAssert.Contains(content, "<td>20</td>");
            StringAssert.Contains(content, "<td>16</td>");
            StringAssert.Contains(content, "<td>13</td>");
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

}
