using BPRadar.Web.Data;
using BPRadar.Web.Features.Baselines;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class BaselineServiceTests
{
    [TestMethod]
    public async Task Marking_profile_default_unsets_previous_default_for_organization()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstOrganization = new Organization { Name = "Contoso" };
        var secondOrganization = new Organization { Name = "Fabrikam" };
        database.Context.AddRange(firstOrganization, secondOrganization);
        await database.Context.SaveChangesAsync();

        var first = await BaselineService.CreateProfileAsync(
            database.Context,
            firstOrganization.Id,
            "First",
            null,
            isDefault: true);
        var otherOrganizationProfile = await BaselineService.CreateProfileAsync(
            database.Context,
            secondOrganization.Id,
            "Other organization",
            null,
            isDefault: true);
        var second = await BaselineService.CreateProfileAsync(
            database.Context,
            firstOrganization.Id,
            "Second",
            null,
            isDefault: true);

        Assert.IsNull(first.Errors);
        Assert.IsNull(second.Errors);
        Assert.IsNull(otherOrganizationProfile.Errors);
        var profiles = await database.Context.BaselineProfiles
            .AsNoTracking()
            .OrderBy(profile => profile.OrganizationId)
            .ThenBy(profile => profile.Name)
            .ToArrayAsync();
        Assert.AreEqual(
            second.EntityId,
            profiles.Single(profile =>
                profile.OrganizationId == firstOrganization.Id &&
                profile.IsDefault).Id);
        Assert.IsFalse(profiles.Single(profile => profile.Id == first.EntityId).IsDefault);
        Assert.IsTrue(profiles.Single(
            profile => profile.Id == otherOrganizationProfile.EntityId).IsDefault);
    }

    [TestMethod]
    [DataRow("-0.01")]
    [DataRow("100.01")]
    public async Task Target_rejects_compliance_percent_outside_range(
        string percentText)
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateProfileAndFrameworkAsync();

        var result = await BaselineService.CreateTargetAsync(
            database.Context,
            setup.OrganizationId,
            setup.ProfileId,
            setup.FrameworkId,
            domainId: null,
            decimal.Parse(percentText),
            targetScore: null,
            notes: null);

        Assert.IsNotNull(result.Errors);
        StringAssert.Contains(
            result.Errors["TargetCompliancePercent"].Single(),
            "between 0 and 100");
        Assert.AreEqual(0, await database.Context.BaselineTargets.CountAsync());
    }

    [TestMethod]
    public async Task Duplicate_target_returns_friendly_validation_error()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateProfileAndFrameworkAsync();
        var first = await BaselineService.CreateTargetAsync(
            database.Context,
            setup.OrganizationId,
            setup.ProfileId,
            setup.FrameworkId,
            domainId: null,
            90m,
            targetScore: null,
            notes: null);

        var duplicate = await BaselineService.CreateTargetAsync(
            database.Context,
            setup.OrganizationId,
            setup.ProfileId,
            setup.FrameworkId,
            domainId: null,
            95m,
            targetScore: null,
            notes: null);

        Assert.IsNull(first.Errors);
        Assert.IsNotNull(duplicate.Errors);
        Assert.AreEqual(
            "A target already exists for this profile, framework, and domain.",
            duplicate.Errors["Target"].Single());
        Assert.AreEqual(1, await database.Context.BaselineTargets.CountAsync());
    }

    [TestMethod]
    public async Task Domain_target_rejects_domain_from_different_framework()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateProfileAndFrameworkAsync();
        var otherFramework = CreateFramework("Other");
        database.Context.Frameworks.Add(otherFramework);
        await database.Context.SaveChangesAsync();

        var result = await BaselineService.CreateTargetAsync(
            database.Context,
            setup.OrganizationId,
            setup.ProfileId,
            setup.FrameworkId,
            otherFramework.Domains.Single().Id,
            80m,
            75m,
            notes: null);

        Assert.IsNotNull(result.Errors);
        StringAssert.Contains(
            result.Errors["DomainId"].Single(),
            "must belong to the selected framework");
        Assert.AreEqual(0, await database.Context.BaselineTargets.CountAsync());
    }

    [TestMethod]
    public async Task Domain_target_rejects_score_outside_normalized_range()
    {
        await using var database = await TestDatabase.CreateAsync();
        var setup = await database.CreateProfileAndFrameworkAsync();
        var domainId = await database.Context.Domains
            .Where(domain => domain.FrameworkId == setup.FrameworkId)
            .Select(domain => domain.Id)
            .SingleAsync();

        var result = await BaselineService.CreateTargetAsync(
            database.Context,
            setup.OrganizationId,
            setup.ProfileId,
            setup.FrameworkId,
            domainId,
            targetCompliancePercent: null,
            targetScore: 100.01m,
            notes: null);

        Assert.IsNotNull(result.Errors);
        StringAssert.Contains(
            result.Errors["TargetScore"].Single(),
            "between 0 and 100");
        Assert.AreEqual(0, await database.Context.BaselineTargets.CountAsync());
    }

    private static Framework CreateFramework(string name)
    {
        var framework = new Framework
        {
            Name = name,
            Version = "1.0",
            Description = "Test framework"
        };
        framework.Domains.Add(new Domain
        {
            Code = $"{name}-DOMAIN",
            Name = $"{name} domain",
            SortOrder = 1
        });
        return framework;
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

        public async Task<BaselineSetup> CreateProfileAndFrameworkAsync()
        {
            var organization = new Organization { Name = "Contoso" };
            var framework = CreateFramework("Primary");
            var now = DateTime.UtcNow;
            var profile = new BaselineProfile
            {
                Organization = organization,
                Name = "Internal target",
                CreatedAt = now,
                UpdatedAt = now
            };
            Context.AddRange(framework, profile);
            await Context.SaveChangesAsync();
            return new BaselineSetup(
                organization.Id,
                profile.Id,
                framework.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record BaselineSetup(
        int OrganizationId,
        int ProfileId,
        int FrameworkId);
}
