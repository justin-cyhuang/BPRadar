using System.Net;
using System.Net.Http.Json;
using BPRadar.Web.Features.IssueMatching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BPRadar.Tests.Features.IssueMatching;

[TestClass]
public sealed class IssueMatchingEndpointTests
{
    [TestMethod]
    public async Task PostCandidates_ReturnsMatchesWithoutLiveNetworkCall()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/issue-matching/candidates",
            new IssueMatchRequest(
                "Customer records could not be restored.",
                "The backup failed and restore testing had not succeeded."));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IssueMatchResult>();
        Assert.IsNotNull(result);
        Assert.IsTrue(
            result.Candidates.Any(
                candidate =>
                    candidate.FrameworkCode == "ISO27001_2022" &&
                    candidate.ControlCode == "A.8.13"));
    }

    private sealed class TestWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IKeywordExtractionService>();
                services.AddSingleton<IKeywordExtractionService>(
                    new StubKeywordExtractionService(["backup failure"]));
            });
        }
    }

    private sealed class StubKeywordExtractionService(
        IReadOnlyList<string> keywords) : IKeywordExtractionService
    {
        public Task<IReadOnlyList<string>> ExtractKeywordsAsync(
            string rootCauseText,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(keywords);
        }
    }
}
