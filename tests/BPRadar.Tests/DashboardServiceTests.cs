using BPRadar.Web.Data;
using BPRadar.Web.Features.Dashboard;
using BPRadar.Web.Features.Surveys;
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
    public async Task Radar_compares_selected_assessments_on_framework_axes_with_target_reference()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = await database.CreateAssessmentAsync(
        [
            ComplianceStatus.Compliant,
            ComplianceStatus.Partial,
            ComplianceStatus.NonCompliant
        ]);
        var organization = await database.Context.Organizations
            .SingleAsync(organization => organization.Id == first.OrganizationId);
        var secondFramework = new Framework
        {
            Name = "Zulu Framework",
            Version = "2.0",
            Description = "Second radar framework"
        };
        var secondDomain = new Domain
        {
            Code = "ZULU",
            Name = "Zulu Domain",
            SortOrder = 1
        };
        var secondControl = new Control
        {
            Code = "ZULU-1",
            Title = "Zulu control",
            Description = "Zulu description",
            SortOrder = 1
        };
        secondDomain.Controls.Add(secondControl);
        secondFramework.Domains.Add(secondDomain);
        var secondAssessment = new Assessment
        {
            Organization = organization,
            Framework = secondFramework,
            Label = "Zulu review",
            SnapshotDate = new DateTime(2026, 8, 1),
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 2)
        };
        secondAssessment.Results.Add(new AssessmentResult
        {
            Control = secondControl,
            Status = ComplianceStatus.Compliant,
            Source = ResultSource.Manual,
            UpdatedAt = new DateTime(2026, 8, 2)
        });
        var profile = new BaselineProfile
        {
            Organization = organization,
            Name = "Radar target",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        profile.Targets.Add(new BaselineTarget
        {
            FrameworkId = first.FrameworkId,
            TargetCompliancePercent = 80m
        });
        profile.Targets.Add(new BaselineTarget
        {
            Framework = secondFramework,
            TargetCompliancePercent = 90m
        });
        database.Context.AddRange(secondAssessment, profile);
        await database.Context.SaveChangesAsync();

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(
                first.OrganizationId,
                [first.AssessmentId, secondAssessment.Id],
                profile.Id));

        var firstFrameworkName = await database.Context.Frameworks
            .Where(framework => framework.Id == first.FrameworkId)
            .Select(framework => framework.Name)
            .SingleAsync();
        CollectionAssert.AreEqual(
            new[]
            {
                $"{firstFrameworkName} 1.0",
                "Zulu Framework 2.0"
            },
            dashboard.Radar.Axes.Select(axis => axis.Label).ToArray());
        var firstSeries = dashboard.Radar.Series
            .Single(series => series.AssessmentId == first.AssessmentId);
        CollectionAssert.AreEqual(new[] { 50m, 0m }, firstSeries.Values);
        var secondSeries = dashboard.Radar.Series
            .Single(series => series.AssessmentId == secondAssessment.Id);
        CollectionAssert.AreEqual(new[] { 0m, 100m }, secondSeries.Values);
        CollectionAssert.AreEqual(
            new[] { 80m, 90m },
            dashboard.Radar.TargetSeries!.Values);
        CollectionAssert.AreEqual(
            new[] { 25m, 50m, 75m, 100m },
            dashboard.Radar.GridLevels);
    }

    [TestMethod]
    public async Task Radar_target_preserves_missing_framework_target_as_undefined()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = await database.CreateAssessmentAsync([ComplianceStatus.Compliant]);
        var organization = await database.Context.Organizations
            .SingleAsync(organization => organization.Id == first.OrganizationId);
        var secondFramework = new Framework
        {
            Name = "Untargeted Framework",
            Version = "1.0",
            Description = "Framework without a configured target"
        };
        var secondAssessment = new Assessment
        {
            Organization = organization,
            Framework = secondFramework,
            Label = "Untargeted review",
            SnapshotDate = new DateTime(2026, 8, 1),
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 2)
        };
        var profile = new BaselineProfile
        {
            Organization = organization,
            Name = "Partial target",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        profile.Targets.Add(new BaselineTarget
        {
            FrameworkId = first.FrameworkId,
            TargetCompliancePercent = 70m
        });
        database.Context.AddRange(secondAssessment, profile);
        await database.Context.SaveChangesAsync();

        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(
                first.OrganizationId,
                [first.AssessmentId, secondAssessment.Id],
                profile.Id));

        CollectionAssert.AreEqual(
            new decimal?[] { 70m, null },
            dashboard.Radar.TargetSeries!.Values);
    }

    [TestMethod]
    public async Task Survey_tracking_scores_weighted_history_deltas_domains_and_reuses_cadence()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateAssessmentAsync([ComplianceStatus.Compliant]);
        var organization = await database.Context.Organizations
            .SingleAsync(organization => organization.Id == setup.OrganizationId);
        var framework = await database.Context.Frameworks
            .SingleAsync(framework => framework.Id == setup.FrameworkId);
        var firstDomain = await database.Context.Domains
            .SingleAsync(domain => domain.FrameworkId == setup.FrameworkId);
        var secondDomain = new Domain
        {
            Framework = framework,
            Code = "SECOND",
            Name = "Second domain",
            SortOrder = 2
        };
        var template = new SurveyTemplate
        {
            Name = "Transformation pulse",
            Cadence = SurveyCadence.Quarterly,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var firstQuestion = new SurveyQuestion
        {
            Code = "PULSE-1",
            Prompt = "First capability",
            Domain = firstDomain,
            Weight = 1m,
            SortOrder = 1,
            IsRequired = true
        };
        var secondQuestion = new SurveyQuestion
        {
            Code = "PULSE-2",
            Prompt = "Second capability",
            Domain = secondDomain,
            Weight = 3m,
            SortOrder = 2,
            IsRequired = true
        };
        template.Questions.Add(firstQuestion);
        template.Questions.Add(secondQuestion);
        var previous = new SurveySubmission
        {
            Organization = organization,
            SurveyTemplate = template,
            Label = "Q2 pulse",
            SnapshotDate = new DateTime(2026, 4, 1),
            SubmittedAt = new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc),
            Notes = "Previous notes"
        };
        previous.Responses.Add(new SurveyResponse
        {
            SurveyQuestion = firstQuestion,
            ResponseLevel = SurveyResponseLevel.VeryLow
        });
        previous.Responses.Add(new SurveyResponse
        {
            SurveyQuestion = secondQuestion,
            ResponseLevel = SurveyResponseLevel.High
        });
        var latest = new SurveySubmission
        {
            Organization = organization,
            SurveyTemplate = template,
            Label = "Q3 pulse",
            SnapshotDate = new DateTime(2026, 7, 1),
            SubmittedAt = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc),
            Notes = "Latest notes"
        };
        latest.Responses.Add(new SurveyResponse
        {
            SurveyQuestion = firstQuestion,
            ResponseLevel = SurveyResponseLevel.Low,
            Score = 100m
        });
        latest.Responses.Add(new SurveyResponse
        {
            SurveyQuestion = secondQuestion,
            ResponseLevel = SurveyResponseLevel.VeryHigh
        });
        database.Context.AddRange(previous, latest);
        await database.Context.SaveChangesAsync();

        var currentDate = new DateTime(2026, 8, 13);
        var cadence = await SurveyCadenceService.GetStatusAsync(
            database.Context,
            setup.OrganizationId,
            template.Id,
            currentDate);
        var dashboard = await DashboardService.GetAsync(
            database.Context,
            new DashboardRequest(
                setup.OrganizationId,
                [setup.AssessmentId],
                SurveyTemplateId: template.Id,
                CurrentDate: currentDate));

        Assert.AreEqual(template.Id, dashboard.SelectedSurveyTemplateId);
        var tracking = dashboard.SurveyTracking!;
        Assert.AreEqual(100m, tracking.LatestScore);
        Assert.AreEqual(43.75m, tracking.LatestDelta);
        Assert.AreEqual(cadence!.Status, tracking.CadenceStatus);
        Assert.AreEqual(new DateTime(2026, 10, 1), tracking.NextDueDate);
        CollectionAssert.AreEqual(
            new[] { latest.Id, previous.Id },
            tracking.History.Select(item => item.SubmissionId).ToArray());
        CollectionAssert.AreEqual(
            new[] { 56.25m, 100m },
            tracking.Trend.Select(point => point.Score).ToArray());
        CollectionAssert.AreEqual(
            new[] { 100m, 25m },
            tracking.DomainDeltas.Select(domain => domain.Delta).ToArray());
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
