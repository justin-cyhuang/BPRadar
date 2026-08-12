using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BPRadar.Web.Features.IssueMatching;

namespace BPRadar.Tests.Features.IssueMatching;

[TestClass]
public sealed class GitHubModelsKeywordExtractionServiceTests
{
    [TestMethod]
    public async Task ExtractKeywordsAsync_ReturnsStructuredModelKeywords()
    {
        var handler = new RecordingHttpMessageHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"keywords\":[\"missing redundancy\",\"single point of failure\"]}"
                  }
                }
              ]
            }
            """);
        var httpClient = new HttpClient(handler);
        var options = new GitHubModelsOptions
        {
            Endpoint = "https://models.example.test/chat/completions",
            Model = "test-model",
            Token = "test-token"
        };
        var service = new GitHubModelsKeywordExtractionService(httpClient, options);

        var keywords = await service.ExtractKeywordsAsync(
            "One instance served the critical workload.");

        CollectionAssert.AreEqual(
            new[] { "missing redundancy", "single point of failure" },
            keywords.ToArray());
        Assert.AreEqual(options.Endpoint, handler.RequestUri?.ToString());
        Assert.AreEqual(
            new AuthenticationHeaderValue("Bearer", "test-token"),
            handler.Authorization);
        StringAssert.Contains(handler.RequestBody, "\"model\":\"test-model\"");
        StringAssert.Contains(
            handler.RequestBody,
            "One instance served the critical workload.");
    }

    [TestMethod]
    public async Task ExtractKeywordsAsync_RejectsMalformedModelContent()
    {
        var handler = new RecordingHttpMessageHandler(
            """
            {
              "choices": [
                { "message": { "content": "not valid JSON" } }
              ]
            }
            """);
        var service = new GitHubModelsKeywordExtractionService(
            new HttpClient(handler),
            new GitHubModelsOptions
            {
                Endpoint = "https://models.example.test/chat/completions",
                Token = "test-token"
            });

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.ExtractKeywordsAsync("Backups did not run."));
    }

    private sealed class RecordingHttpMessageHandler(string responseBody)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public AuthenticationHeaderValue? Authorization { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
