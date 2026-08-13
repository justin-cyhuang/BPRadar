using System.Net.Http.Headers;
using BPRadar.Web.Features.IssueMatching;

namespace BPRadar.Tests.Features.IssueMatching;

[TestClass]
public sealed class OpenAICompatibleKeywordExtractionServiceTests
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
        var options = new OpenAICompatibleOptions
        {
            Endpoint = "https://llm.example.test/v1/chat/completions",
            Model = "test-model",
            ApiKey = "test-key"
        };
        var service = new OpenAICompatibleKeywordExtractionService(
            new HttpClient(handler),
            options);

        var keywords = await service.ExtractKeywordsAsync(
            "One instance served the critical workload.");

        CollectionAssert.AreEqual(
            new[] { "missing redundancy", "single point of failure" },
            keywords.ToArray());
        Assert.AreEqual(options.Endpoint, handler.RequestUri?.ToString());
        Assert.AreEqual(
            new AuthenticationHeaderValue("Bearer", "test-key"),
            handler.Authorization);
        StringAssert.Contains(handler.RequestBody, "\"model\":\"test-model\"");
        StringAssert.Contains(
            handler.RequestBody,
            "One instance served the critical workload.");
    }

    [TestMethod]
    public async Task ExtractKeywordsAsync_UsesConfiguredApiKeyHeader()
    {
        var handler = new RecordingHttpMessageHandler(
            """
            {
              "choices": [
                { "message": { "content": "{\"keywords\":[\"restore failure\"]}" } }
              ]
            }
            """);
        var service = new OpenAICompatibleKeywordExtractionService(
            new HttpClient(handler),
            new OpenAICompatibleOptions
            {
                Endpoint = "https://azure.example.test/chat/completions",
                ApiKey = "azure-key",
                ApiKeyHeaderName = "api-key",
                AuthScheme = null
            });

        await service.ExtractKeywordsAsync("Restore tests failed.");

        Assert.AreEqual("azure-key", handler.GetHeaderValue("api-key"));
        Assert.IsNull(handler.Authorization);
    }

    [TestMethod]
    public async Task ExtractKeywordsAsync_OmitsAuthenticationWhenApiKeyIsAbsent()
    {
        var handler = new RecordingHttpMessageHandler(
            """
            {
              "choices": [
                { "message": { "content": "{\"keywords\":[\"capacity limit\"]}" } }
              ]
            }
            """);
        var service = new OpenAICompatibleKeywordExtractionService(
            new HttpClient(handler),
            new OpenAICompatibleOptions
            {
                Endpoint = "http://localhost:11434/v1/chat/completions",
                ApiKey = null
            });

        await service.ExtractKeywordsAsync("The service exceeded capacity.");

        Assert.IsNull(handler.Authorization);
        Assert.IsNull(handler.GetHeaderValue("api-key"));
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
        var service = new OpenAICompatibleKeywordExtractionService(
            new HttpClient(handler),
            new OpenAICompatibleOptions
            {
                Endpoint = "https://llm.example.test/v1/chat/completions"
            });

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.ExtractKeywordsAsync("Backups did not run."));
    }

    [TestMethod]
    public async Task ExtractKeywordsAsync_RejectsAuthorizationWithoutScheme()
    {
        var service = new OpenAICompatibleKeywordExtractionService(
            new HttpClient(new RecordingHttpMessageHandler("{}")),
            new OpenAICompatibleOptions
            {
                Endpoint = "https://llm.example.test/v1/chat/completions",
                ApiKey = "test-key",
                ApiKeyHeaderName = "Authorization",
                AuthScheme = null
            });

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.ExtractKeywordsAsync("Backups did not run."));

        StringAssert.Contains(exception.Message, "AuthScheme");
    }

}
