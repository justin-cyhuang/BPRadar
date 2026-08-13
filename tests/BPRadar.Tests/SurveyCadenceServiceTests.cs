using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class SurveyCadenceServiceTests
{
    [TestMethod]
    public async Task Active_template_without_a_submission_is_due_for_the_organization()
    {
        await using var database = await TestDatabase.CreateAsync();
        var dbContext = database.DbContext;
        var organization = new Organization { Name = "Contoso" };
        var template = CreateTemplate(SurveyCadence.Quarterly);
        dbContext.AddRange(organization, template);
        await dbContext.SaveChangesAsync();

        var dueTemplates = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organization.Id,
            new DateTime(2026, 8, 13));

        Assert.HasCount(1, dueTemplates);
        Assert.AreEqual(template.Id, dueTemplates[0].TemplateId);
        Assert.IsNull(dueTemplates[0].LastSubmittedSnapshotDate);
        Assert.AreEqual(SurveyDueStatus.Overdue, dueTemplates[0].Status);
    }

    [TestMethod]
    public async Task Quarterly_template_becomes_due_three_months_after_the_latest_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var dbContext = database.DbContext;
        var organization = new Organization { Name = "Contoso" };
        var template = CreateTemplate(SurveyCadence.Quarterly);
        dbContext.AddRange(organization, template);
        await dbContext.SaveChangesAsync();
        dbContext.SurveySubmissions.Add(new SurveySubmission
        {
            OrganizationId = organization.Id,
            SurveyTemplateId = template.Id,
            Label = "2026 Q1 pulse",
            SnapshotDate = new DateTime(2026, 4, 30),
            SubmittedAt = new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        var onTime = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organization.Id,
            new DateTime(2026, 7, 15));
        var dueSoon = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organization.Id,
            new DateTime(2026, 7, 16));
        var due = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organization.Id,
            new DateTime(2026, 7, 30));
        var overdue = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organization.Id,
            new DateTime(2026, 7, 31));

        Assert.IsEmpty(onTime);
        Assert.HasCount(1, dueSoon);
        Assert.AreEqual(SurveyDueStatus.DueSoon, dueSoon[0].Status);
        Assert.HasCount(1, due);
        Assert.AreEqual(SurveyDueStatus.DueSoon, due[0].Status);
        Assert.HasCount(1, overdue);
        Assert.AreEqual(new DateTime(2026, 7, 30), overdue[0].NextDueDate);
        Assert.AreEqual(SurveyDueStatus.Overdue, overdue[0].Status);
    }

    [TestMethod]
    public async Task Inactive_templates_are_not_due()
    {
        await using var database = await TestDatabase.CreateAsync();
        var dbContext = database.DbContext;
        var organization = new Organization { Name = "Contoso" };
        var template = CreateTemplate(SurveyCadence.Quarterly);
        template.IsActive = false;
        dbContext.AddRange(organization, template);
        await dbContext.SaveChangesAsync();

        var dueTemplates = await SurveyCadenceService.ListDueAsync(
            dbContext,
            organization.Id,
            new DateTime(2026, 8, 13));

        Assert.IsEmpty(dueTemplates);
    }

    private sealed class TestDatabase(
        SqliteConnection connection,
        BPRadarDbContext dbContext) : IAsyncDisposable
    {
        public BPRadarDbContext DbContext { get; } = dbContext;

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<BPRadarDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new BPRadarDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private static SurveyTemplate CreateTemplate(SurveyCadence cadence) =>
        new()
        {
            Name = $"Pulse {Guid.NewGuid():N}",
            Cadence = cadence,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
