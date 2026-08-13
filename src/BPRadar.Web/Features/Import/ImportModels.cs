using BPRadar.Web.Data;

namespace BPRadar.Web.Features.Import;

public sealed record ImportColumnDefinition(
    string Name,
    bool Required,
    string Group,
    string[] Aliases);

public static class ImportColumns
{
    public static readonly IReadOnlyList<ImportColumnDefinition> All =
    [
        new("OrganizationName", true, "Assessment metadata", ["Organization", "Organization Name"]),
        new("FrameworkCode", true, "Assessment metadata", ["Framework", "Framework Code"]),
        new("FrameworkVersion", false, "Assessment metadata", ["Framework Version", "Version"]),
        new("AssessmentLabel", true, "Assessment metadata", ["Assessment", "Assessment Label"]),
        new("AssessmentSnapshotDate", true, "Assessment metadata", ["Snapshot Date", "Assessment Date"]),
        new("BaselineProfileName", false, "Assessment metadata", ["Baseline", "Baseline Profile"]),
        new("ControlCode", true, "Control result", ["Control", "Control Code", "Control ID"]),
        new("DomainCode", false, "Control result", ["Domain", "Domain Code"]),
        new("ControlTitle", false, "Control result", ["Title", "Control Title"]),
        new("Status", true, "Control result", ["Compliance Status", "Result"]),
        new("Score", false, "Control result", ["Numeric Score"]),
        new("ScoreScale", false, "Control result", ["Score Scale", "Scale"]),
        new("EvidenceUrl", false, "Control result", ["Evidence", "Evidence URL"]),
        new("Notes", false, "Control result", ["Comments", "Comment"]),
        new("ExternalRecordId", false, "Control result", ["External ID", "Record ID"])
    ];

    public static ImportColumnDefinition Get(string name) =>
        All.Single(column => column.Name == name);
}

public sealed record ImportTable(
    IReadOnlyList<string> Headers,
    IReadOnlyList<ImportTableRow> Rows);

public sealed record ImportTableRow(
    int RowNumber,
    IReadOnlyList<string> Values);

public sealed record ImportFileMetadata(
    string Name,
    long Size,
    string Sha256);

public sealed record ImportBatch(
    Guid Id,
    int AssessmentId,
    ImportFileMetadata File,
    ImportTable Table,
    IReadOnlyDictionary<string, string?> SuggestedMapping,
    ImportPreview? Preview = null);

public sealed record ImportPreview(
    Guid ImportBatchId,
    int AssessmentId,
    ImportFileMetadata File,
    int RowsRead,
    int ValidRows,
    int InvalidRows,
    int RowsToCreate,
    int RowsToUpdate,
    IReadOnlyList<ImportPreviewRow> Rows,
    IReadOnlyList<ImportValidationError> Errors);

public sealed record ImportPreviewRow(
    int RowNumber,
    int ControlId,
    string ControlCode,
    ComplianceStatus Status,
    decimal? Score,
    string? EvidenceUrl,
    string? Notes,
    string? ExternalRecordId,
    bool WillCreate);

public sealed record ImportValidationError(
    int RowNumber,
    string ReasonCode,
    string Message);

public sealed record ImportCommitResult(
    Guid ImportBatchId,
    int RowsRead,
    int ValidRows,
    int InvalidRows,
    int UpsertedRows);
