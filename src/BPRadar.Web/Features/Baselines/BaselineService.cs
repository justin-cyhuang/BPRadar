using System.Diagnostics;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Baselines;

public static class BaselineService
{
    public static async Task<BaselineOperationResult> CreateProfileAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        string name,
        string? description,
        bool isDefault,
        CancellationToken cancellationToken = default)
    {
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Baselines",
            "BaselineProfileCreateStarted",
            $"organizationId={organizationId}");

        var errors = await ValidateProfileAsync(
            dbContext,
            organizationId,
            name,
            cancellationToken);
        if (errors.Count > 0)
        {
            return BaselineOperationResult.Invalid(errors);
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (isDefault)
        {
            await UnsetDefaultProfilesAsync(
                dbContext,
                organizationId,
                exceptProfileId: null,
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        var profile = new BaselineProfile
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = TextNormalization.EmptyToNull(description),
            IsDefault = isDefault,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.BaselineProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        WriteCompleted("BaselineProfileCreated", organizationId, profile.Id);
        return BaselineOperationResult.Success(profile.Id);
    }

    public static async Task<BaselineOperationResult> UpdateProfileAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int profileId,
        string name,
        string? description,
        bool isDefault,
        CancellationToken cancellationToken = default)
    {
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Baselines",
            "BaselineProfileUpdateStarted",
            $"organizationId={organizationId} profileId={profileId}");

        var errors = await ValidateProfileAsync(
            dbContext,
            organizationId,
            name,
            cancellationToken);
        if (errors.Count > 0)
        {
            return BaselineOperationResult.Invalid(errors);
        }

        var profile = await dbContext.BaselineProfiles.SingleOrDefaultAsync(
            item => item.Id == profileId && item.OrganizationId == organizationId,
            cancellationToken);
        if (profile is null)
        {
            return BaselineOperationResult.Invalid(
                "Profile",
                "The baseline profile was not found.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (isDefault)
        {
            await UnsetDefaultProfilesAsync(
                dbContext,
                organizationId,
                profileId,
                cancellationToken);
        }

        profile.Name = name.Trim();
        profile.Description = TextNormalization.EmptyToNull(description);
        profile.IsDefault = isDefault;
        profile.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        WriteCompleted("BaselineProfileUpdated", organizationId, profile.Id);
        return BaselineOperationResult.Success(profile.Id);
    }

    public static async Task<BaselineOperationResult> DeleteProfileAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.BaselineProfiles.SingleOrDefaultAsync(
            item => item.Id == profileId && item.OrganizationId == organizationId,
            cancellationToken);
        if (profile is null)
        {
            return BaselineOperationResult.Invalid(
                "Profile",
                "The baseline profile was not found.");
        }

        dbContext.BaselineProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteCompleted("BaselineProfileDeleted", organizationId, profileId);
        return BaselineOperationResult.Success(profileId);
    }

    public static async Task<BaselineOperationResult> CreateTargetAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int profileId,
        int frameworkId,
        int? domainId,
        decimal? targetCompliancePercent,
        decimal? targetScore,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var errors = await ValidateTargetAsync(
            dbContext,
            organizationId,
            profileId,
            frameworkId,
            domainId,
            targetCompliancePercent,
            targetScore,
            exceptTargetId: null,
            cancellationToken);
        if (errors.Count > 0)
        {
            return BaselineOperationResult.Invalid(errors);
        }

        var target = new BaselineTarget
        {
            BaselineProfileId = profileId,
            FrameworkId = frameworkId,
            DomainId = domainId,
            TargetCompliancePercent = targetCompliancePercent,
            TargetScore = targetScore,
            Notes = TextNormalization.EmptyToNull(notes)
        };
        dbContext.BaselineTargets.Add(target);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(target).State = EntityState.Detached;
            return DuplicateTargetResult();
        }

        WriteCompleted("BaselineTargetCreated", organizationId, profileId, target.Id);
        return BaselineOperationResult.Success(target.Id);
    }

    public static async Task<BaselineOperationResult> UpdateTargetAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int profileId,
        int targetId,
        int frameworkId,
        int? domainId,
        decimal? targetCompliancePercent,
        decimal? targetScore,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var target = await dbContext.BaselineTargets.SingleOrDefaultAsync(
            item =>
                item.Id == targetId &&
                item.BaselineProfileId == profileId &&
                item.BaselineProfile.OrganizationId == organizationId,
            cancellationToken);
        if (target is null)
        {
            return BaselineOperationResult.Invalid(
                "Target",
                "The baseline target was not found.");
        }

        var errors = await ValidateTargetAsync(
            dbContext,
            organizationId,
            profileId,
            frameworkId,
            domainId,
            targetCompliancePercent,
            targetScore,
            targetId,
            cancellationToken);
        if (errors.Count > 0)
        {
            return BaselineOperationResult.Invalid(errors);
        }

        target.FrameworkId = frameworkId;
        target.DomainId = domainId;
        target.TargetCompliancePercent = targetCompliancePercent;
        target.TargetScore = targetScore;
        target.Notes = TextNormalization.EmptyToNull(notes);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return DuplicateTargetResult();
        }

        WriteCompleted("BaselineTargetUpdated", organizationId, profileId, target.Id);
        return BaselineOperationResult.Success(target.Id);
    }

    public static async Task<BaselineOperationResult> DeleteTargetAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int profileId,
        int targetId,
        CancellationToken cancellationToken = default)
    {
        var target = await dbContext.BaselineTargets.SingleOrDefaultAsync(
            item =>
                item.Id == targetId &&
                item.BaselineProfileId == profileId &&
                item.BaselineProfile.OrganizationId == organizationId,
            cancellationToken);
        if (target is null)
        {
            return BaselineOperationResult.Invalid(
                "Target",
                "The baseline target was not found.");
        }

        dbContext.BaselineTargets.Remove(target);
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteCompleted("BaselineTargetDeleted", organizationId, profileId, targetId);
        return BaselineOperationResult.Success(targetId);
    }

    private static async Task<Dictionary<string, string[]>> ValidateProfileAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        string name,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["Name"] = ["A profile name is required."];
        }
        else if (name.Trim().Length > 200)
        {
            errors["Name"] = ["Profile name cannot exceed 200 characters."];
        }

        if (!await dbContext.Organizations.AnyAsync(
                organization => organization.Id == organizationId,
                cancellationToken))
        {
            errors["Organization"] = ["The organization was not found."];
        }

        return errors;
    }

    private static async Task<Dictionary<string, string[]>> ValidateTargetAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int profileId,
        int frameworkId,
        int? domainId,
        decimal? targetCompliancePercent,
        decimal? targetScore,
        int? exceptTargetId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (targetCompliancePercent is < 0 or > 100)
        {
            errors["TargetCompliancePercent"] =
                ["Target compliance percent must be between 0 and 100."];
        }

        if (targetScore is < 0 or > 100)
        {
            errors["TargetScore"] =
                ["Target score must be between 0 and 100."];
        }

        if (domainId is null && targetCompliancePercent is null)
        {
            errors["TargetCompliancePercent"] =
                ["A framework-level target requires a compliance percent."];
        }
        else if (domainId is not null &&
                 targetCompliancePercent is null &&
                 targetScore is null)
        {
            errors["Target"] =
                ["A domain-level target requires a compliance percent or target score."];
        }

        if (!await dbContext.BaselineProfiles.AnyAsync(
                profile =>
                    profile.Id == profileId &&
                    profile.OrganizationId == organizationId,
                cancellationToken))
        {
            errors["Profile"] = ["The baseline profile was not found."];
        }

        if (!await dbContext.Frameworks.AnyAsync(
                framework => framework.Id == frameworkId,
                cancellationToken))
        {
            errors["FrameworkId"] = ["The framework was not found."];
        }

        if (domainId is not null &&
            !await dbContext.Domains.AnyAsync(
                domain =>
                    domain.Id == domainId &&
                    domain.FrameworkId == frameworkId,
                cancellationToken))
        {
            errors["DomainId"] =
                ["The selected domain must belong to the selected framework."];
        }

        var duplicateExists = await dbContext.BaselineTargets.AnyAsync(
            target =>
                target.BaselineProfileId == profileId &&
                target.FrameworkId == frameworkId &&
                target.DomainId == domainId &&
                (!exceptTargetId.HasValue || target.Id != exceptTargetId.Value),
            cancellationToken);
        if (duplicateExists)
        {
            errors["Target"] = [DuplicateTargetMessage];
        }

        return errors;
    }

    private static async Task UnsetDefaultProfilesAsync(
        BPRadarDbContext dbContext,
        int organizationId,
        int? exceptProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await dbContext.BaselineProfiles
            .Where(profile =>
                profile.OrganizationId == organizationId &&
                profile.IsDefault &&
                (!exceptProfileId.HasValue || profile.Id != exceptProfileId.Value))
            .ToArrayAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var profile in profiles)
        {
            profile.IsDefault = false;
            profile.UpdatedAt = now;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19
        };

    private static BaselineOperationResult DuplicateTargetResult() =>
        BaselineOperationResult.Invalid("Target", DuplicateTargetMessage);

    private static void WriteCompleted(
        string operation,
        int organizationId,
        int profileId,
        int? targetId = null)
    {
        var details = $"organizationId={organizationId} profileId={profileId}";
        if (targetId.HasValue)
        {
            details += $" targetId={targetId.Value}";
        }

        BPRadarTrace.Write(
            TraceEventType.Information,
            "Baselines",
            operation,
            details);
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Baselines",
            $"{operation}Completed",
            details);
    }

    private const string DuplicateTargetMessage =
        "A target already exists for this profile, framework, and domain.";
}

public sealed record BaselineOperationResult(
    int? EntityId,
    Dictionary<string, string[]>? Errors)
{
    public static BaselineOperationResult Success(int entityId) =>
        new(entityId, null);

    public static BaselineOperationResult Invalid(string key, string message) =>
        Invalid(new Dictionary<string, string[]> { [key] = [message] });

    public static BaselineOperationResult Invalid(
        Dictionary<string, string[]> errors) =>
        new(null, errors);
}
