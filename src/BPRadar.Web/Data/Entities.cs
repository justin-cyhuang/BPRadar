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
