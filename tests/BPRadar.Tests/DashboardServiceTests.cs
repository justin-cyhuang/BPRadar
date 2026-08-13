using BPRadar.Web.Data;
using BPRadar.Web.Features.Dashboard;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class DashboardServiceTests
{
    [TestMethod]
    public async Task Overview_uses_specified_completion_compliance_and_gap_formulas()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateAssessmentAsync(
        [
            ComplianceStatus.Compliant,
            ComplianceStatus.Partial,
            ComplianceStatus.NonCompliant,
            ComplianceStatus.NotApplicable,
            ComplianceStatus.NotAssessed
        ]);

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(setup.OrganizationId, [setup.AssessmentId]));

        var overview = dashboard.Overviews.Single();
        Assert.AreEqual(80m, overview.CompletionPercent);
        Assert.AreEqual(25m, overview.CompliancePercent);
        Assert.AreEqual(2, overview.GapCount);
    }

    [TestMethod]
    public async Task Overview_subtracts_framework_target_from_actual_compliance()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateAssessmentAsync(
        [
            ComplianceStatus.Compliant,
            ComplianceStatus.Compliant,
            ComplianceStatus.Partial,
            ComplianceStatus.NonCompliant
        ]);
        var profile = new BaselineProfile
        {
            OrganizationId = setup.OrganizationId,
            Name = "Internal target",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        profile.Targets.Add(new BaselineTarget
        {
            FrameworkId = setup.FrameworkId,
            TargetCompliancePercent = 80m
        });
        database.Context.BaselineProfiles.Add(profile);
        await database.Context.SaveChangesAsync();

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(
                setup.OrganizationId,
                [setup.AssessmentId],
                profile.Id));

        var overview = dashboard.Overviews.Single();
        Assert.AreEqual(50m, overview.CompliancePercent);
        Assert.AreEqual(80m, overview.TargetCompliancePercent);
        Assert.AreEqual(-30m, overview.TargetDelta);
    }

    [TestMethod]
    public async Task Gap_filter_combines_framework_domain_and_status_and_sorts_rows()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateAssessmentAsync(
        [
            ComplianceStatus.Partial,
            ComplianceStatus.Compliant,
            ComplianceStatus.NonCompliant
        ]);
        var domainId = await database.Context.Domains
            .Where(domain => domain.FrameworkId == setup.FrameworkId)
            .Select(domain => domain.Id)
            .SingleAsync();
        var alternateDomain = new Domain
        {
            FrameworkId = setup.FrameworkId,
            Code = "ALT",
            Name = "Alternate Domain",
            SortOrder = 2
        };
        var alternateControl = new Control
        {
            Code = "ALT-1",
            Title = "Alternate control",
            Description = "Alternate description",
            SortOrder = 1
        };
        alternateDomain.Controls.Add(alternateControl);
        var alternateResult = new AssessmentResult
        {
            AssessmentId = setup.AssessmentId,
            Control = alternateControl,
            Status = ComplianceStatus.NonCompliant,
            Source = ResultSource.Manual,
            UpdatedAt = DateTime.UtcNow
        };
        var otherFramework = new Framework
        {
            Name = "Other Framework",
            Version = "1.0",
            Description = "Other framework"
        };
        var otherDomain = new Domain
        {
            Code = "OTHER",
            Name = "Other Domain",
            SortOrder = 1
        };
        var otherControl = new Control
        {
            Code = "OTHER-1",
            Title = "Other control",
            Description = "Other description",
            SortOrder = 1
        };
        otherDomain.Controls.Add(otherControl);
        otherFramework.Domains.Add(otherDomain);
        var otherAssessment = new Assessment
        {
            OrganizationId = setup.OrganizationId,
            Framework = otherFramework,
            Label = "Other review",
            SnapshotDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        otherAssessment.Results.Add(new AssessmentResult
        {
            Control = otherControl,
            Status = ComplianceStatus.NonCompliant,
            Source = ResultSource.Manual,
            UpdatedAt = DateTime.UtcNow
        });
        database.Context.AddRange(
            alternateDomain,
            alternateResult,
            otherAssessment);
        await database.Context.SaveChangesAsync();

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(
                setup.OrganizationId,
                [setup.AssessmentId, otherAssessment.Id],
                FrameworkId: setup.FrameworkId,
                DomainId: domainId,
                GapStatus: ComplianceStatus.NonCompliant,
                Sort: DashboardGapSort.Score,
                SortDescending: true));

        Assert.HasCount(1, dashboard.Gaps);
        var gap = dashboard.Gaps.Single();
        Assert.AreEqual("TEST-3", gap.ControlCode);
        Assert.AreEqual(ComplianceStatus.NonCompliant, gap.Status);
        Assert.AreEqual(20m, gap.Score);
        Assert.AreEqual("Note 3", gap.Notes);
    }

    [TestMethod]
    public async Task Empty_scope_selects_latest_assessment_per_framework_and_default_profile()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateAssessmentAsync([ComplianceStatus.Partial]);
        var current = await database.Context.Assessments.SingleAsync();
        current.SnapshotDate = new DateTime(2026, 7, 1);
        var newer = new Assessment
        {
            OrganizationId = setup.OrganizationId,
            FrameworkId = setup.FrameworkId,
            Label = "Newer review",
            SnapshotDate = new DateTime(2026, 8, 1),
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 2)
        };
        var secondFramework = new Framework
        {
            Name = "Second Framework",
            Version = "2.0",
            Description = "Second dashboard framework"
        };
        secondFramework.Domains.Add(new Domain
        {
            Code = "SECOND",
            Name = "Second Domain",
            SortOrder = 1
        });
        var secondAssessment = new Assessment
        {
            OrganizationId = setup.OrganizationId,
            Framework = secondFramework,
            Label = "Second framework review",
            SnapshotDate = new DateTime(2026, 6, 1),
            CreatedAt = new DateTime(2026, 6, 1),
            UpdatedAt = new DateTime(2026, 6, 2)
        };
        var defaultProfile = new BaselineProfile
        {
            OrganizationId = setup.OrganizationId,
            Name = "Default target",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        database.Context.AddRange(newer, secondAssessment, defaultProfile);
        await database.Context.SaveChangesAsync();

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(setup.OrganizationId, []));

        CollectionAssert.AreEquivalent(
            new[] { newer.Id, secondAssessment.Id },
            dashboard.SelectedAssessmentIds);
        Assert.AreEqual(defaultProfile.Id, dashboard.SelectedBaselineProfileId);
        Assert.IsFalse(dashboard.SelectedAssessmentIds.Contains(setup.AssessmentId));
    }

    [TestMethod]
    public async Task Cross_organization_assessment_ids_fall_back_to_organization_defaults()
    {
        await using var database = await TestDatabase.CreateAsync();
        var requestedOrganization =
            await database.CreateAssessmentAsync([ComplianceStatus.Compliant]);
        var otherOrganization =
            await database.CreateAssessmentAsync([ComplianceStatus.Partial]);

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(
                requestedOrganization.OrganizationId,
                [otherOrganization.AssessmentId]));

        CollectionAssert.AreEqual(
            new[] { requestedOrganization.AssessmentId },
            dashboard.SelectedAssessmentIds);
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

        public async Task<DashboardSetup> CreateAssessmentAsync(
            IReadOnlyList<ComplianceStatus> statuses)
        {
            var organization = new Organization { Name = "Contoso" };
            var framework = new Framework
            {
                Name = $"Test Framework {Guid.NewGuid():N}",
                Version = "1.0",
                Description = "Dashboard test framework"
            };
            var domain = new Domain
            {
                Code = "TEST",
                Name = "Test Domain",
                SortOrder = 1
            };
            framework.Domains.Add(domain);
            var now = new DateTime(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc);
            var assessment = new Assessment
            {
                Organization = organization,
                Framework = framework,
                Label = "Current review",
                SnapshotDate = now.Date,
                CreatedAt = now,
                UpdatedAt = now
            };

            for (var index = 0; index < statuses.Count; index++)
            {
                var control = new Control
                {
                    Code = $"TEST-{index + 1}",
                    Title = $"Control {index + 1}",
                    Description = $"Description {index + 1}",
                    SortOrder = index + 1
                };
                domain.Controls.Add(control);
                assessment.Results.Add(new AssessmentResult
                {
                    Control = control,
                    Status = statuses[index],
                    Score = index * 10m,
                    Notes = $"Note {index + 1}",
                    Source = ResultSource.Manual,
                    UpdatedAt = now
                });
            }

            Context.Assessments.Add(assessment);
            await Context.SaveChangesAsync();
            return new DashboardSetup(
                organization.Id,
                framework.Id,
                assessment.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record DashboardSetup(
        int OrganizationId,
        int FrameworkId,
        int AssessmentId);
}
