using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BPRadar.Web.Features.Assessments;

public static class AssessmentService
{
    public static async Task<AssessmentCreateResult> CreateAsync(
        BPRadarDbContext dbContext,
        CreateAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Assessments",
            "AssessmentCreateStarted",
            $"organizationId={request.OrganizationId} frameworkId={request.FrameworkId}");

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return AssessmentCreateResult.Invalid(
                "Label",
                "An assessment label is required.");
        }

        var snapshotDate = request.SnapshotDate.Date;
        if (request.SnapshotDate == default || snapshotDate > DateTime.UtcNow.Date)
        {
            return AssessmentCreateResult.Invalid(
                "SnapshotDate",
                "Snapshot date must be valid and cannot be later than the current UTC date.");
        }

        var newOrganizationName = request.NewOrganizationName?.Trim();
        var hasExistingOrganization = request.OrganizationId is > 0;
        var hasNewOrganization = !string.IsNullOrWhiteSpace(newOrganizationName);
        if (hasExistingOrganization == hasNewOrganization)
        {
            return AssessmentCreateResult.Invalid(
                "Organization",
                "Select an existing organization or enter a new organization name.");
        }

        var framework = await dbContext.Frameworks
            .Include(item => item.Domains)
            .ThenInclude(domain => domain.Controls)
            .SingleOrDefaultAsync(
                item => item.Id == request.FrameworkId,
                cancellationToken);
        if (framework is null)
        {
            return AssessmentCreateResult.Invalid(
                "FrameworkId",
                $"Framework {request.FrameworkId} does not exist.");
        }

        Organization organization;
        if (hasExistingOrganization)
        {
            organization = await dbContext.Organizations.SingleOrDefaultAsync(
                item => item.Id == request.OrganizationId,
                cancellationToken)
                ?? null!;
            if (organization is null)
            {
                return AssessmentCreateResult.Invalid(
                    "OrganizationId",
                    $"Organization {request.OrganizationId} does not exist.");
            }
        }
        else
        {
            organization = new Organization { Name = newOrganizationName! };
        }

        var now = DateTime.UtcNow;
        var assessment = new Assessment
        {
            Organization = organization,
            Framework = framework,
            Label = request.Label.Trim(),
            SnapshotDate = snapshotDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var control in framework.Domains.SelectMany(domain => domain.Controls))
        {
            assessment.Results.Add(new AssessmentResult
            {
                Control = control,
                Status = ComplianceStatus.NotAssessed,
                Source = ResultSource.Manual,
                UpdatedAt = now
            });
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var traceDetails =
            $"organizationId={assessment.OrganizationId} assessmentId={assessment.Id} " +
            $"frameworkId={assessment.FrameworkId} results={assessment.Results.Count}";
        BPRadarTrace.Write(
            TraceEventType.Information,
            "Assessments",
            "AssessmentCreated",
            traceDetails);
        BPRadarTrace.Write(
            TraceEventType.Stop,
            "Assessments",
            "AssessmentCreateCompleted",
            traceDetails);

        return AssessmentCreateResult.Success(
            new AssessmentDetail(
                assessment.Id,
                assessment.OrganizationId,
                organization.Name,
                assessment.FrameworkId,
                framework.Name,
                assessment.Label,
                assessment.SnapshotDate,
                assessment.Results.Count));
    }

    public static async Task<AssessmentSummary[]> ListAsync(
        BPRadarDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Assessments
            .AsNoTracking()
            .OrderByDescending(assessment => assessment.SnapshotDate)
            .ThenByDescending(assessment => assessment.CreatedAt)
            .Select(assessment => new
            {
                assessment.Id,
                assessment.OrganizationId,
                OrganizationName = assessment.Organization.Name,
                assessment.FrameworkId,
                FrameworkName = assessment.Framework.Name,
                FrameworkVersion = assessment.Framework.Version,
                assessment.Label,
                assessment.SnapshotDate,
                AssessedCount = assessment.Results.Count(
                    result => result.Status != ComplianceStatus.NotAssessed),
                ControlCount = dbContext.Controls.Count(
                    control => control.Domain.FrameworkId == assessment.FrameworkId)
            })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => new AssessmentSummary(
                row.Id,
                row.OrganizationId,
                row.OrganizationName,
                row.FrameworkId,
                row.FrameworkName,
                row.FrameworkVersion,
                row.Label,
                row.SnapshotDate,
                row.ControlCount == 0
                    ? 0m
                    : row.AssessedCount * 100m / row.ControlCount))
            .ToArray();
    }
}

public sealed record CreateAssessmentRequest(
    int? OrganizationId,
    string? NewOrganizationName,
    int FrameworkId,
    string Label,
    DateTime SnapshotDate);

public sealed record AssessmentDetail(
    int Id,
    int OrganizationId,
    string OrganizationName,
    int FrameworkId,
    string FrameworkName,
    string Label,
    DateTime SnapshotDate,
    int ResultCount);

public sealed record AssessmentSummary(
    int Id,
    int OrganizationId,
    string OrganizationName,
    int FrameworkId,
    string FrameworkName,
    string FrameworkVersion,
    string Label,
    DateTime SnapshotDate,
    decimal CompletionPercent);

public sealed record AssessmentCreateResult(
    AssessmentDetail? Assessment,
    Dictionary<string, string[]>? Errors)
{
    public static AssessmentCreateResult Success(AssessmentDetail assessment) =>
        new(assessment, null);

    public static AssessmentCreateResult Invalid(string key, string message) =>
        new(null, new Dictionary<string, string[]> { [key] = [message] });
}
