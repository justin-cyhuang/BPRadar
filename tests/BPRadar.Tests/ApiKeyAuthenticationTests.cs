using System.Net;
using BPRadar.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BPRadar.Tests;

[TestClass]
public sealed class ApiKeyAuthenticationTests
{
    private const string ExpectedApiKey = "test-api-key";

    [TestMethod]
    public async Task StartupFailsWhenApiKeyIsRequiredButMissing()
    {
        await using var application = ApiKeyApplication.Create(
            requireApiKey: true,
            apiKey: null);

        var exception = Assert.Throws<OptionsValidationException>(
            application.CreateClient);

        StringAssert.Contains(
            exception.Message,
            "Api:ApiKey must be configured");
    }

    [TestMethod]
    public async Task ApiRequestReturnsUnauthorizedWhenRequiredKeyIsMissing()
    {
        await using var application = ApiKeyApplication.Create(
            requireApiKey: true,
            ExpectedApiKey);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/admin/survey-templates");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiRequestReturnsUnauthorizedWhenRequiredKeyIsIncorrect()
    {
        await using var application = ApiKeyApplication.Create(
            requireApiKey: true,
            ExpectedApiKey);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-api-key");

        var response = await client.GetAsync("/api/admin/survey-templates");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiRequestSucceedsWhenRequiredKeyIsCorrect()
    {
        await using var application = ApiKeyApplication.Create(
            requireApiKey: true,
            ExpectedApiKey);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ExpectedApiKey);

        var response = await client.GetAsync("/api/admin/survey-templates");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiRequestSucceedsWithoutKeyWhenRequirementIsDisabled()
    {
        await using var application = ApiKeyApplication.Create(
            requireApiKey: false,
            apiKey: null);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/admin/survey-templates");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task RazorPageDoesNotRequireApiKey()
    {
        await using var application = ApiKeyApplication.Create(
            requireApiKey: true,
            ExpectedApiKey);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/Dashboard");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class ApiKeyApplication(
        string databasePath,
        WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        private readonly string databasePath = databasePath;
        private readonly WebApplicationFactory<Program> factory = factory;

        public static ApiKeyApplication Create(
            bool requireApiKey,
            string? apiKey)
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-api-auth-{Guid.NewGuid():N}.db");
            var settings = new Dictionary<string, string?>
            {
                ["Api:RequireApiKey"] = requireApiKey.ToString(),
                ["Api:ApiKey"] = apiKey
            };
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration(
                        (_, configuration) =>
                            configuration.AddInMemoryCollection(settings));
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<DbContextOptions<BPRadarDbContext>>();
                        services.AddDbContext<BPRadarDbContext>(
                            options => options.UseSqlite(
                                $"Data Source={databasePath};Pooling=False"));
                    });
                });
            return new ApiKeyApplication(databasePath, factory);
        }

        public HttpClient CreateClient() => factory.CreateClient();

        public async ValueTask DisposeAsync()
        {
            await factory.DisposeAsync();
            File.Delete(databasePath);
        }
    }
}
