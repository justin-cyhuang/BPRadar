using System.Text.Json.Serialization;
using BPRadar.Web.Data;

namespace BPRadar.Web.Features.Surveys;

internal sealed record FrameworkSeedFile(
    FrameworkSeed Framework,
    IReadOnlyList<DomainSeed> Domains);

internal sealed record FrameworkSeed(
    string Code,
    string Name,
    string Version,
    string Description,
    string? SourceUrl);

internal sealed record DomainSeed(
    string Code,
    string Name,
    int SortOrder,
    IReadOnlyList<ControlSeed> Controls);

internal sealed record ControlSeed(
    string Code,
    string Title,
    string Description,
    int SortOrder,
    string? GuidanceUrl);

internal sealed record SurveySeedFile(
    SurveyTemplateSeed SurveyTemplate,
    IReadOnlyList<SurveyQuestionSeed> Questions);

internal sealed record SurveyTemplateSeed(
    string Name,
    string FrameworkCode,
    string? Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SurveyCadence>))]
    SurveyCadence Cadence,
    bool IsActive);

internal sealed record SurveyQuestionSeed(
    string Code,
    string Prompt,
    string DomainCode,
    string ControlCode,
    decimal? Weight,
    int SortOrder,
    bool IsRequired);
