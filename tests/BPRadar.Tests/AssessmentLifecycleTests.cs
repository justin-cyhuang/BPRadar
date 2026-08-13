using BPRadar.Web.Data;
using BPRadar.Web.Features.Assessments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class AssessmentLifecycleTests
{
    [TestMethod]
    public async Task Assessment_and_baseline_entities_can_be_created()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        var organization = new Organization { Name = "Contoso" };
        var framework = CreateFramework(controlCount: 1);
        var domain = framework.Domains.Single();
        var control = domain.Controls.Single();
        var profile = new BaselineProfile
        {
            Organization = organization,
            Name = "2026 Internal Target",
            Description = "Target posture for the year",
            IsDefault = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        profile.Targets.Add(new BaselineTarget
        {
            Framework = framework,
            Domain = domain,
            TargetCompliancePercent = 90m,
            TargetScore = 0.8m,
            Notes = "Security target"
        });
        var assessment = new Assessment
        {
            Organization = organization,
            Framework = framework,
            BaselineProfile = profile,
            Label = "2026 Q1 Security Review",
            SnapshotDate = now.Date,
            CreatedAt = now,
            UpdatedAt = now
        };
        assessment.Results.Add(new AssessmentResult
        {
            Control = control,
            Status = ComplianceStatus.Partial,
            Score = 50m,
            Notes = "Work in progress",
            EvidenceUrl = "https://example.test/evidence",
            Source = ResultSource.Manual,
            UpdatedAt = now
        });

        database.Context.Assessments.Add(assessment);
        await database.Context.SaveChangesAsync();

        var saved = await database.Context.Assessments
            .AsNoTracking()
            .Include(item => item.Organization)
            .Include(item => item.BaselineProfile)
            .ThenInclude(item => item!.Targets)
            .Include(item => item.Results)
            .SingleAsync();
        Assert.AreEqual("Contoso", saved.Organization.Name);
        Assert.AreEqual("2026 Internal Target", saved.BaselineProfile!.Name);
        Assert.HasCount(1, saved.BaselineProfile.Targets);
        Assert.AreEqual(90m, saved.BaselineProfile.Targets.Single().TargetCompliancePercent);
        Assert.HasCount(1, saved.Results);
        Assert.AreEqual(ComplianceStatus.Partial, saved.Results.Single().Status);
        Assert.AreEqual(ResultSource.Manual, saved.Results.Single().Source);
    }

    [TestMethod]
    public async Task Create_auto_populates_one_not_assessed_result_per_framework_control()
    {
        await using var database = await TestDatabase.CreateAsync();
        var organization = new Organization { Name = "Contoso" };
        var framework = CreateFramework(controlCount: 3);
        database.Context.AddRange(organization, framework);
        await database.Context.SaveChangesAsync();

        var result = await AssessmentService.CreateAsync(
            database.Context,
            new CreateAssessmentRequest(
                organization.Id,
                null,
                framework.Id,
                "2026 Q1 Security Review",
                DateTime.UtcNow.Date));

        Assert.IsNull(result.Errors);
        Assert.IsNotNull(result.Assessment);
        Assert.AreEqual(3, result.Assessment.ResultCount);
        var assessment = await database.Context.Assessments
            .AsNoTracking()
            .Include(item => item.Results)
            .SingleAsync(item => item.Id == result.Assessment.Id);
        Assert.HasCount(3, assessment.Results);
        Assert.IsTrue(assessment.Results.All(item =>
            item.Status == ComplianceStatus.NotAssessed &&
            item.Source == ResultSource.Manual));
        CollectionAssert.AreEquivalent(
            framework.Domains.SelectMany(domain => domain.Controls)
                .Select(control => control.Id)
                .ToArray(),
            assessment.Results.Select(item => item.ControlId).ToArray());
    }

    [TestMethod]
    public async Task Assessment_result_rejects_duplicate_assessment_and_control()
    {
        await using var database = await TestDatabase.CreateAsync();
        var organization = new Organization { Name = "Contoso" };
        var framework = CreateFramework(controlCount: 1);
        var assessment = CreateAssessment(
            organization,
            framework,
            "Security Review");
        database.Context.Assessments.Add(assessment);
        await database.Context.SaveChangesAsync();
        var control = framework.Domains.Single().Controls.Single();
        database.Context.AssessmentResults.AddRange(
            CreateResult(assessment, control),
            CreateResult(assessment, control));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Baseline_target_rejects_duplicate_framework_level_target()
    {
        await using var database = await TestDatabase.CreateAsync();
        var organization = new Organization { Name = "Contoso" };
        var framework = CreateFramework(controlCount: 1);
        var profile = new BaselineProfile
        {
            Organization = organization,
            Name = "Internal Target",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        database.Context.AddRange(framework, profile);
        await database.Context.SaveChangesAsync();
        database.Context.BaselineTargets.AddRange(
            CreateTarget(profile, framework, domain: null),
            CreateTarget(profile, framework, domain: null));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Baseline_target_rejects_duplicate_domain_target()
    {
        await using var database = await TestDatabase.CreateAsync();
        var organization = new Organization { Name = "Contoso" };
        var framework = CreateFramework(controlCount: 1);
        var profile = new BaselineProfile
        {
            Organization = organization,
            Name = "Internal Target",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        database.Context.AddRange(framework, profile);
        await database.Context.SaveChangesAsync();
        var domain = framework.Domains.Single();
        database.Context.BaselineTargets.AddRange(
            CreateTarget(profile, framework, domain),
            CreateTarget(profile, framework, domain));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    private static Framework CreateFramework(int controlCount)
    {
        var framework = new Framework
        {
            Name = $"Framework {Guid.NewGuid():N}",
            Version = "1.0",
            Description = "Test framework"
        };
        var domain = new Domain
        {
            Code = "SEC",
            Name = "Security",
            SortOrder = 1
        };
        framework.Domains.Add(domain);
        for (var index = 1; index <= controlCount; index++)
        {
            domain.Controls.Add(new Control
            {
                Code = $"SEC-{index:00}",
                Title = $"Control {index}",
                Description = $"Test control {index}",
                SortOrder = index
            });
        }

        return framework;
    }

    private static Assessment CreateAssessment(
        Organization organization,
        Framework framework,
        string label)
    {
        var now = DateTime.UtcNow;
        return new Assessment
        {
            Organization = organization,
            Framework = framework,
            Label = label,
            SnapshotDate = now.Date,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static AssessmentResult CreateResult(
        Assessment assessment,
        Control control) =>
        new()
        {
            Assessment = assessment,
            Control = control,
            Status = ComplianceStatus.NotAssessed,
            Source = ResultSource.Manual,
            UpdatedAt = DateTime.UtcNow
        };

    private static BaselineTarget CreateTarget(
        BaselineProfile profile,
        Framework framework,
        Domain? domain) =>
        new()
        {
            BaselineProfile = profile,
            Framework = framework,
            Domain = domain,
            TargetCompliancePercent = 90m
        };

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

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
