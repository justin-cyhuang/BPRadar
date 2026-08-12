namespace BPRadar.Web.Data;

public sealed class Framework
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Description { get; set; }
    public string? SourceUrl { get; set; }
    public ICollection<Domain> Domains { get; } = [];
}

public sealed class Domain
{
    public int Id { get; set; }
    public int FrameworkId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public Framework Framework { get; set; } = null!;
    public ICollection<Control> Controls { get; } = [];
}

public sealed class Control
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? GuidanceUrl { get; set; }
    public int SortOrder { get; set; }
    public Domain Domain { get; set; } = null!;
}

public enum SurveyCadence
{
    Monthly,
    Quarterly,
    SemiAnnual,
    Annual
}

public sealed class SurveyTemplate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? FrameworkId { get; set; }
    public string? Description { get; set; }
    public SurveyCadence Cadence { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Framework? Framework { get; set; }
    public ICollection<SurveyQuestion> Questions { get; } = [];
}

public sealed class SurveyQuestion
{
    public int Id { get; set; }
    public int SurveyTemplateId { get; set; }
    public required string Code { get; set; }
    public required string Prompt { get; set; }
    public int? FrameworkId { get; set; }
    public int? DomainId { get; set; }
    public int? ControlId { get; set; }
    public decimal Weight { get; set; } = 1.0m;
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public SurveyTemplate SurveyTemplate { get; set; } = null!;
    public Framework? Framework { get; set; }
    public Domain? Domain { get; set; }
    public Control? Control { get; set; }
}

public sealed class Organization
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public ICollection<SurveySubmission> SurveySubmissions { get; } = [];
}

public sealed class SurveySubmission
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int SurveyTemplateId { get; set; }
    public required string Label { get; set; }
    public DateTime SnapshotDate { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? Notes { get; set; }
    public Organization Organization { get; set; } = null!;
    public SurveyTemplate SurveyTemplate { get; set; } = null!;
    public ICollection<SurveyResponse> Responses { get; } = [];
}

public enum SurveyResponseLevel
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh,
    NotApplicable
}

public sealed class SurveyResponse
{
    public int Id { get; set; }
    public int SurveySubmissionId { get; set; }
    public int SurveyQuestionId { get; set; }
    public SurveyResponseLevel ResponseLevel { get; set; }
    public decimal? Score { get; set; }
    public string? Notes { get; set; }
    public SurveySubmission SurveySubmission { get; set; } = null!;
    public SurveyQuestion SurveyQuestion { get; set; } = null!;
}
