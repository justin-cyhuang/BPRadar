using BPRadar.Web.Features.IssueMatching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BPRadar.Tests.Features.IssueMatching;

[TestClass]
public sealed class IssueMatchingServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddIssueMatching_UsesConfiguredDefaultProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IssueMatching:LlmProvider"] = "GitHubModels",
                ["IssueMatching:GitHubModels:Token"] = "test-token"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddIssueMatching(configuration, AppContext.BaseDirectory);

        using var provider = services.BuildServiceProvider();
        Assert.IsInstanceOfType<GitHubModelsKeywordExtractionService>(
            provider.GetRequiredService<IKeywordExtractionService>());
        Assert.IsInstanceOfType<IssueMatchingService>(
            provider.GetRequiredService<IIssueMatchingService>());
    }

    [TestMethod]
    public void AddIssueMatching_UsesConfiguredOpenAICompatibleProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IssueMatching:LlmProvider"] = "OpenAICompatible",
                ["IssueMatching:OpenAICompatible:Endpoint"] =
                    "https://llm.example.test/v1/chat/completions",
                ["IssueMatching:OpenAICompatible:Model"] = "test-model"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddIssueMatching(configuration, AppContext.BaseDirectory);

        using var provider = services.BuildServiceProvider();
        Assert.IsInstanceOfType<OpenAICompatibleKeywordExtractionService>(
            provider.GetRequiredService<IKeywordExtractionService>());
    }
}
