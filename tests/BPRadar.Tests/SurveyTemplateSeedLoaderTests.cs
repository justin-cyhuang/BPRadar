using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class SurveyTemplateSeedLoaderTests
{
    [TestMethod]
    public async Task SeedAsync_loads_templates_and_resolves_every_question_mapping()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var dbContext = database.Context;

        await DatabaseSeeder.SeedAsync(
            dbContext,
            Path.Combine(AppContext.BaseDirectory, "seed-data"));

        var templates = await dbContext.SurveyTemplates
            .Include(template => template.Questions)
            .OrderBy(template => template.Name)
            .ToListAsync();

        Assert.HasCount(3, templates);
        var countsByTemplate = templates.ToDictionary(
            template => template.Name,
            template => template.Questions.Count);
        Assert.AreEqual(20, countsByTemplate["Azure WAF Transformation Pulse"]);
        Assert.AreEqual(16, countsByTemplate["ISO 27001 ISMS Transformation Pulse"]);
        Assert.AreEqual(13, countsByTemplate["ISO 20000-1 SMS Transformation Pulse"]);
        Assert.IsTrue(templates
            .SelectMany(template => template.Questions)
            .All(question =>
                question.FrameworkId.HasValue &&
                question.DomainId.HasValue &&
                question.ControlId.HasValue));
    }

    [TestMethod]
    public async Task SeedAsync_can_be_re_run_without_duplicating_templates_or_questions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var dbContext = database.Context;
        var seedDataPath = Path.Combine(AppContext.BaseDirectory, "seed-data");

        await DatabaseSeeder.SeedAsync(dbContext, seedDataPath);
        await DatabaseSeeder.SeedAsync(dbContext, seedDataPath);

        Assert.AreEqual(3, await dbContext.SurveyTemplates.CountAsync());
        Assert.AreEqual(49, await dbContext.SurveyQuestions.CountAsync());
    }

    [TestMethod]
    public async Task SeedAsync_fails_loudly_when_a_question_control_code_is_unmatched()
    {
        await using var seedData = TemporarySeedData.Create();
        var surveyPath = seedData.SurveyPath("waf-survey-template.json");
        var content = await File.ReadAllTextAsync(surveyPath);
        await File.WriteAllTextAsync(
            surveyPath,
            content.Replace("\"RE:04\"", "\"RE:404\"", StringComparison.Ordinal));
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => DatabaseSeeder.SeedAsync(database.Context, seedData.RootPath));

        StringAssert.Contains(exception.Message, "unmatched control 'RE:404'");
    }

    [TestMethod]
    public async Task SeedAsync_defaults_an_omitted_question_weight_to_one()
    {
        await using var seedData = TemporarySeedData.Create();
        var surveyPath = seedData.SurveyPath("waf-survey-template.json");
        var content = await File.ReadAllTextAsync(surveyPath);
        await File.WriteAllTextAsync(
            surveyPath,
            content.Replace(
                "\"controlCode\": \"RE:04\", \"weight\": 1.0,",
                "\"controlCode\": \"RE:04\",",
                StringComparison.Ordinal));
        await using var database = await SqliteTestDatabase.CreateAsync();

        await DatabaseSeeder.SeedAsync(database.Context, seedData.RootPath);

        var question = await database.Context.SurveyQuestions.SingleAsync(
            item => item.Code == "SVY-WAF-RE-01");
        Assert.AreEqual(1.0m, question.Weight);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var sourcePath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(
                destination,
                Path.GetRelativePath(source, sourcePath));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }
    }

    private sealed class TemporarySeedData(string rootPath) : IAsyncDisposable
    {
        public string RootPath { get; } = rootPath;

        public static TemporarySeedData Create()
        {
            var source = Path.Combine(AppContext.BaseDirectory, "seed-data");
            var destination = Path.Combine(
                Path.GetTempPath(),
                $"bpradar-seed-{Guid.NewGuid():N}");
            CopyDirectory(source, destination);
            return new TemporarySeedData(destination);
        }

        public string SurveyPath(string fileName) =>
            Path.Combine(RootPath, "survey", fileName);

        public ValueTask DisposeAsync()
        {
            Directory.Delete(RootPath, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SqliteTestDatabase(
        SqliteConnection connection,
        BPRadarDbContext context) : IAsyncDisposable
    {
        public BPRadarDbContext Context { get; } = context;

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<BPRadarDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new BPRadarDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new SqliteTestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
