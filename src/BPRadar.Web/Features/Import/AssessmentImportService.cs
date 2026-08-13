using System.Diagnostics;
using System.Globalization;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Features.Import;

public sealed class AssessmentImportService(
    BPRadarDbContext dbContext,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlyDictionary<string, ComplianceStatus> Statuses =
        new Dictionary<string, ComplianceStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["Compliant"] = ComplianceStatus.Compliant,
            ["Partial"] = ComplianceStatus.Partial,
            ["NonCompliant"] = ComplianceStatus.NonCompliant,
            ["NotApplicable"] = ComplianceStatus.NotApplicable,
            ["NotAssessed"] = ComplianceStatus.NotAssessed,
            ["Yes"] = ComplianceStatus.Compliant,
            ["No"] = ComplianceStatus.NonCompliant,
            ["N/A"] = ComplianceStatus.NotApplicable,
            ["Not Assessed"] = ComplianceStatus.NotAssessed
        };

    private static readonly IReadOnlyDictionary<string, string> FrameworkCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Azure Well-Architected Framework"] = "AZURE_WAF",
            ["ISO/IEC 27001"] = "ISO27001_2022",
            ["ISO/IEC 20000-1"] = "ISO20000_1"
        };

    public async Task<ImportPreview> PreviewAsync(
        ImportBatch batch,
        IReadOnlyDictionary<string, string?> mapping,
        CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        WriteTrace(
            TraceEventType.Start,
            "ImportPreviewStarted",
            batch,
            "rowsRead=0 valid=0 invalid=0 upserted=0");

        var assessment = await dbContext.Assessments
            .AsNoTracking()
            .Include(item => item.Organization)
            .Include(item => item.Framework)
            .Include(item => item.BaselineProfile)
            .Include(item => item.Results)
            .SingleOrDefaultAsync(
                item => item.Id == batch.AssessmentId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Assessment {batch.AssessmentId} does not exist.");
        var controls = await dbContext.Controls
            .AsNoTracking()
            .Where(control => control.Domain.FrameworkId == assessment.FrameworkId)
            .Select(control => new ControlReference(
                control.Id,
                control.Code,
                control.Title,
                control.Domain.Code))
            .ToArrayAsync(cancellationToken);
        var controlsByCode = controls.ToDictionary(
            control => control.Code,
            StringComparer.OrdinalIgnoreCase);
        var existingControlIds = assessment.Results
            .Select(result => result.ControlId)
            .ToHashSet();
        var indexes = ResolveIndexes(batch.Table.Headers, mapping);
        var rows = new List<ImportPreviewRow>();
        var errors = new List<ImportValidationError>();

        foreach (var sourceRow in batch.Table.Rows)
        {
            var rowErrors = ValidateRow(
                sourceRow,
                indexes,
                assessment,
                controlsByCode,
                existingControlIds,
                out var previewRow);
            errors.AddRange(rowErrors);
            if (rowErrors.Count == 0)
            {
                rows.Add(previewRow!);
            }
        }

        foreach (var error in errors)
        {
            WriteTrace(
                TraceEventType.Warning,
                "ImportRowInvalid",
                batch,
                $"row={error.RowNumber} reason={error.ReasonCode}");
        }

        var preview = new ImportPreview(
            batch.Id,
            batch.AssessmentId,
            batch.File,
            batch.Table.Rows.Count,
            rows.Count,
            errors.Select(error => error.RowNumber).Distinct().Count(),
            rows.Count(row => row.WillCreate),
            rows.Count(row => !row.WillCreate),
            rows,
            errors);
        WriteTrace(
            TraceEventType.Stop,
            "ImportPreviewCompleted",
            batch,
            Counts(preview, upserted: 0),
            timer.ElapsedMilliseconds);
        return preview;
    }

    public async Task<ImportCommitResult> CommitAsync(
        ImportBatch batch,
        CancellationToken cancellationToken = default)
    {
        var preview = batch.Preview
            ?? throw new InvalidOperationException(
                "The import must be previewed before it can be confirmed.");
        var timer = Stopwatch.StartNew();
        WriteTrace(
            TraceEventType.Start,
            "ImportCommitStarted",
            batch,
            Counts(preview, upserted: 0));

        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var assessment = await dbContext.Assessments
                .Include(item => item.Results)
                .SingleOrDefaultAsync(
                    item => item.Id == batch.AssessmentId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Assessment {batch.AssessmentId} does not exist.");
            var existingByControl = assessment.Results.ToDictionary(
                result => result.ControlId);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var upsertedControlIds = new HashSet<int>();

            foreach (var row in preview.Rows)
            {
                if (!existingByControl.TryGetValue(row.ControlId, out var result))
                {
                    result = new AssessmentResult
                    {
                        Assessment = assessment,
                        ControlId = row.ControlId
                    };
                    assessment.Results.Add(result);
                    existingByControl[row.ControlId] = result;
                }

                result.Status = row.Status;
                result.Score = row.Score;
                result.EvidenceUrl = EmptyToNull(row.EvidenceUrl);
                result.Notes = EmptyToNull(row.Notes);
                result.ExternalRecordId = EmptyToNull(row.ExternalRecordId);
                result.Source = ResultSource.Import;
                result.UpdatedAt = now;
                upsertedControlIds.Add(row.ControlId);
            }

            assessment.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var commitResult = new ImportCommitResult(
                batch.Id,
                preview.RowsRead,
                preview.ValidRows,
                preview.InvalidRows,
                upsertedControlIds.Count);
            WriteTrace(
                TraceEventType.Stop,
                "ImportCommitCompleted",
                batch,
                Counts(preview, commitResult.UpsertedRows),
                timer.ElapsedMilliseconds);
            return commitResult;
        }
        catch (Exception exception)
        {
            WriteTrace(
                TraceEventType.Error,
                "ImportCommitFailed",
                batch,
                $"{Counts(preview, upserted: 0)} exceptionType={exception.GetType().Name}",
                timer.ElapsedMilliseconds);
            throw;
        }
    }

    public static bool TryNormalizeStatus(
        string? value,
        out ComplianceStatus status) =>
        Statuses.TryGetValue(value?.Trim() ?? string.Empty, out status);

    public static bool TryNormalizeScore(
        string? value,
        string? scale,
        out decimal? score,
        out string? error)
    {
        score = null;
        error = null;
        var trimmedValue = value?.Trim();
        var trimmedScale = scale?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            if (!string.IsNullOrWhiteSpace(trimmedScale) &&
                !IsKnownScale(trimmedScale))
            {
                error = "ScoreScale must be 0-100 or 0-5.";
                return false;
            }

            return true;
        }

        if (!decimal.TryParse(
            trimmedValue,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed))
        {
            error = "Score must be a number.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(trimmedScale) ||
            trimmedScale.Equals("0-100", StringComparison.OrdinalIgnoreCase))
        {
            if (parsed is < 0 or > 100)
            {
                error = "Score must be between 0 and 100.";
                return false;
            }

            score = parsed;
            return true;
        }

        if (trimmedScale.Equals("0-5", StringComparison.OrdinalIgnoreCase))
        {
            if (parsed is < 0 or > 5)
            {
                error = "Score must be between 0 and 5 for the 0-5 scale.";
                return false;
            }

            score = parsed / 5m * 100m;
            return true;
        }

        error = "ScoreScale must be 0-100 or 0-5.";
        return false;
    }

    private static List<ImportValidationError> ValidateRow(
        ImportTableRow sourceRow,
        IReadOnlyDictionary<string, int> indexes,
        Assessment assessment,
        IReadOnlyDictionary<string, ControlReference> controlsByCode,
        IReadOnlySet<int> existingControlIds,
        out ImportPreviewRow? previewRow)
    {
        previewRow = null;
        var errors = new List<ImportValidationError>();
        string Value(string column) =>
            indexes.TryGetValue(column, out var index) &&
            index >= 0 &&
            index < sourceRow.Values.Count
                ? sourceRow.Values[index].Trim()
                : string.Empty;
        void Error(string code, string message) =>
            errors.Add(new ImportValidationError(
                sourceRow.RowNumber,
                code,
                message));

        foreach (var column in ImportColumns.All.Where(column => column.Required))
        {
            if (string.IsNullOrWhiteSpace(Value(column.Name)))
            {
                Error(
                    $"Required{column.Name}",
                    $"{column.Name} is required.");
            }
        }

        if (!string.Equals(
            Value("OrganizationName"),
            assessment.Organization.Name,
            StringComparison.OrdinalIgnoreCase))
        {
            Error(
                "OrganizationMismatch",
                "OrganizationName does not match the selected assessment.");
        }

        var expectedFrameworkCode = FrameworkCodes.GetValueOrDefault(
            assessment.Framework.Name);
        if (expectedFrameworkCode is null ||
            !string.Equals(
                Value("FrameworkCode"),
                expectedFrameworkCode,
                StringComparison.OrdinalIgnoreCase))
        {
            Error(
                "FrameworkMismatch",
                "FrameworkCode does not match the selected assessment.");
        }

        var frameworkVersion = Value("FrameworkVersion");
        if (!string.IsNullOrWhiteSpace(frameworkVersion) &&
            !string.Equals(
                frameworkVersion,
                assessment.Framework.Version,
                StringComparison.OrdinalIgnoreCase))
        {
            Error(
                "FrameworkVersionMismatch",
                "FrameworkVersion does not match the selected assessment.");
        }

        if (!string.Equals(
            Value("AssessmentLabel"),
            assessment.Label,
            StringComparison.OrdinalIgnoreCase))
        {
            Error(
                "AssessmentLabelMismatch",
                "AssessmentLabel does not match the selected assessment.");
        }

        var snapshotDateValue = Value("AssessmentSnapshotDate");
        if (!DateTime.TryParseExact(
            snapshotDateValue,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var snapshotDate))
        {
            Error(
                "InvalidSnapshotDate",
                "AssessmentSnapshotDate must use yyyy-MM-dd.");
        }
        else if (snapshotDate.Date > DateTime.UtcNow.Date)
        {
            Error(
                "FutureSnapshotDate",
                "AssessmentSnapshotDate cannot be in the future.");
        }
        else if (snapshotDate.Date != assessment.SnapshotDate.Date)
        {
            Error(
                "AssessmentSnapshotDateMismatch",
                "AssessmentSnapshotDate does not match the selected assessment.");
        }

        var baselineName = Value("BaselineProfileName");
        if (!string.IsNullOrWhiteSpace(baselineName) &&
            !string.Equals(
                baselineName,
                assessment.BaselineProfile?.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            Error(
                "BaselineProfileMismatch",
                "BaselineProfileName does not match the selected assessment.");
        }

        var controlCode = Value("ControlCode");
        if (!controlsByCode.TryGetValue(controlCode, out var control))
        {
            Error(
                "UnrecognizedControlCode",
                $"ControlCode '{controlCode}' is not part of the selected assessment framework.");
        }
        else
        {
            var domainCode = Value("DomainCode");
            if (!string.IsNullOrWhiteSpace(domainCode) &&
                !string.Equals(
                    domainCode,
                    control.DomainCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                Error(
                    "DomainCodeMismatch",
                    $"DomainCode does not match control '{control.Code}'.");
            }
        }

        if (!TryNormalizeStatus(Value("Status"), out var status))
        {
            Error(
                "InvalidStatus",
                "Status is not a recognized compliance status.");
        }

        if (!TryNormalizeScore(
            Value("Score"),
            Value("ScoreScale"),
            out var score,
            out var scoreError))
        {
            Error("InvalidScore", scoreError!);
        }

        if (errors.Count > 0 || control is null)
        {
            return errors;
        }

        previewRow = new ImportPreviewRow(
            sourceRow.RowNumber,
            control.Id,
            control.Code,
            status,
            score,
            EmptyToNull(Value("EvidenceUrl")),
            EmptyToNull(Value("Notes")),
            EmptyToNull(Value("ExternalRecordId")),
            !existingControlIds.Contains(control.Id));
        return errors;
    }

    private static IReadOnlyDictionary<string, int> ResolveIndexes(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string?> mapping) =>
        ImportColumns.All.ToDictionary(
            column => column.Name,
            column =>
            {
                mapping.TryGetValue(column.Name, out var header);
                return string.IsNullOrWhiteSpace(header)
                    ? -1
                    : headers
                        .Select((value, index) => new { value, index })
                        .FirstOrDefault(item => string.Equals(
                            item.value,
                            header,
                            StringComparison.OrdinalIgnoreCase))
                        ?.index ?? -1;
            },
            StringComparer.Ordinal);

    private static bool IsKnownScale(string value) =>
        value.Equals("0-100", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("0-5", StringComparison.OrdinalIgnoreCase);

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Counts(ImportPreview preview, int upserted) =>
        $"rowsRead={preview.RowsRead} valid={preview.ValidRows} " +
        $"invalid={preview.InvalidRows} upserted={upserted}";

    private static void WriteTrace(
        TraceEventType severity,
        string operation,
        ImportBatch batch,
        string details,
        long? durationMilliseconds = null)
    {
        var safeFileName = batch.File.Name
            .Replace('\r', '_')
            .Replace('\n', '_');
        BPRadarTrace.Write(
            severity,
            "Import",
            operation,
            $"importBatchId={batch.Id} assessmentId={batch.AssessmentId} " +
            $"fileName=\"{safeFileName}\" fileSize={batch.File.Size} " +
            $"fileSha256={batch.File.Sha256} {details}",
            durationMilliseconds);
    }

    private sealed record ControlReference(
        int Id,
        string Code,
        string Title,
        string DomainCode);
}
