using BPRadar.Web.Data;
using BPRadar.Web.Features.ManualEntry;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class ManualEntryTests
{
    [TestMethod]
    public async Task Upsert_creates_then_updates_the_same_assessment_control_result()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (assessment, control) = await database.CreateAssessmentAsync();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 13, 8, 0, 0, TimeSpan.Zero));

        var created = await ManualEntryService.UpsertAsync(
            database.Context,
            clock,
            assessment.Id,
            control.Id,
            new SaveAssessmentResultRequest(
                "Partial",
                50m,
                "Initial note",
                "https://example.test/evidence"));

        Assert.IsNotNull(created.Result);
        Assert.IsNull(created.Errors);
        Assert.AreEqual(ResultSource.Manual, created.Result.Source);
        Assert.AreEqual(1, created.Result.OverallProgress.Assessed);
        Assert.AreEqual(1, created.Result.OverallProgress.Total);
        var resultId = created.Result.Id;

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var updated = await ManualEntryService.UpsertAsync(
            database.Context,
            clock,
            assessment.Id,
            control.Id,
            new SaveAssessmentResultRequest(
                "Compliant",
                90m,
                "Updated note",
                null));

        Assert.IsNotNull(updated.Result);
        Assert.AreEqual(resultId, updated.Result.Id);
        Assert.AreEqual(ComplianceStatus.Compliant, updated.Result.Status);
        Assert.AreEqual(90m, updated.Result.Score);
        Assert.AreEqual("Updated note", updated.Result.Notes);
        Assert.IsNull(updated.Result.EvidenceUrl);
        Assert.AreEqual(clock.UtcNow.UtcDateTime, updated.Result.UpdatedAt);
        var saved = await database.Context.AssessmentResults.AsNoTracking().ToArrayAsync();
        Assert.HasCount(1, saved);
    }

    [TestMethod]
    public async Task Upsert_rejects_out_of_range_score_and_malformed_evidence_url()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (assessment, control) = await database.CreateAssessmentAsync();

        var outcome = await ManualEntryService.UpsertAsync(
            database.Context,
            TimeProvider.System,
            assessment.Id,
            control.Id,
            new SaveAssessmentResultRequest(
                "Compliant",
                101m,
                null,
                "not a url"));

        Assert.IsNull(outcome.Result);
        Assert.IsNotNull(outcome.Errors);
        Assert.IsTrue(outcome.Errors.ContainsKey("Score"));
        Assert.IsTrue(outcome.Errors.ContainsKey("EvidenceUrl"));
        Assert.IsEmpty(await database.Context.AssessmentResults.ToArrayAsync());
    }

    [TestMethod]
    public async Task Upsert_rejects_a_control_from_a_different_framework()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (assessment, _) = await database.CreateAssessmentAsync();
        var otherFramework = new Framework
        {
            Name = $"Other framework {Guid.NewGuid():N}",
            Version = "1.0",
            Description = "Out-of-scope framework"
        };
        var otherDomain = new Domain
        {
            Code = "OTHER",
            Name = "Other domain",
            SortOrder = 1
        };
        var otherControl = new Control
        {
            Code = "OTHER-1",
            Title = "Other control",
            Description = "A control from another framework",
            SortOrder = 1
        };
        otherDomain.Controls.Add(otherControl);
        otherFramework.Domains.Add(otherDomain);
        database.Context.Frameworks.Add(otherFramework);
        await database.Context.SaveChangesAsync();

        var outcome = await ManualEntryService.UpsertAsync(
            database.Context,
            TimeProvider.System,
            assessment.Id,
            otherControl.Id,
            new SaveAssessmentResultRequest(
                "Compliant",
                100m,
                null,
                null));

        Assert.IsNull(outcome.Result);
        Assert.IsNull(outcome.Errors);
        Assert.IsNotNull(outcome.NotFoundMessage);
        StringAssert.Contains(outcome.NotFoundMessage, "is not part of assessment");
        Assert.IsEmpty(await database.Context.AssessmentResults.ToArrayAsync());
    }

    [TestMethod]
    public void Progress_excludes_not_assessed_and_counts_all_other_statuses()
    {
        var progress = ManualEntryService.CalculateProgress(
        [
            ComplianceStatus.NotAssessed,
            ComplianceStatus.Compliant,
            ComplianceStatus.Partial,
            ComplianceStatus.NonCompliant,
            ComplianceStatus.NotApplicable
        ]);

        Assert.AreEqual(4, progress.Assessed);
        Assert.AreEqual(5, progress.Total);
    }

    [TestMethod]
    public async Task Checklist_groups_and_orders_controls_with_saved_progress()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (assessment, firstControl) = await database.CreateAssessmentAsync(
            includeSecondControl: true);
        database.Context.AssessmentResults.Add(new AssessmentResult
        {
            AssessmentId = assessment.Id,
            ControlId = firstControl.Id,
            Status = ComplianceStatus.Partial,
            Source = ResultSource.Manual,
            UpdatedAt = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var checklist = await ManualEntryService.GetChecklistAsync(
            database.Context,
            assessment.Id);

        Assert.IsNotNull(checklist);
        Assert.AreEqual(1, checklist.Progress.Assessed);
        Assert.AreEqual(2, checklist.Progress.Total);
        Assert.HasCount(1, checklist.Domains);
        CollectionAssert.AreEqual(
            new[] { "CTRL-1", "CTRL-2" },
            checklist.Domains[0].Controls.Select(control => control.Code).ToArray());
        Assert.AreEqual(
            ComplianceStatus.NotAssessed,
            checklist.Domains[0].Controls[1].Status);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class TestDatabase(
        SqliteConnection connection,
        BPRadarDbContext context) : IAsyncDisposable
    {
        private readonly SqliteConnection connection = connection;

        public BPRadarDbContext Context { get; } = context;

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<BPRadarDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new BPRadarDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async Task<(Assessment Assessment, Control FirstControl)>
            CreateAssessmentAsync(bool includeSecondControl = false)
        {
            var organization = new Organization { Name = "Contoso" };
            var framework = new Framework
            {
                Name = $"Framework {Guid.NewGuid():N}",
                Version = "1.0",
                Description = "Manual entry test framework"
            };
            var domain = new Domain
            {
                Code = "DOM",
                Name = "Domain",
                SortOrder = 1
            };
            var firstControl = new Control
            {
                Code = "CTRL-1",
                Title = "First control",
                Description = "First description",
                SortOrder = 1
            };
            domain.Controls.Add(firstControl);
            if (includeSecondControl)
            {
                domain.Controls.Add(new Control
                {
                    Code = "CTRL-2",
                    Title = "Second control",
                    Description = "Second description",
                    SortOrder = 2
                });
            }

            framework.Domains.Add(domain);
            var now = DateTime.UtcNow;
            var assessment = new Assessment
            {
                Organization = organization,
                Framework = framework,
                Label = "Manual review",
                SnapshotDate = now.Date,
                CreatedAt = now,
                UpdatedAt = now
            };
            Context.Assessments.Add(assessment);
            await Context.SaveChangesAsync();
            return (assessment, firstControl);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
