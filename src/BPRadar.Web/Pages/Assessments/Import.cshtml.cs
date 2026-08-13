using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using BPRadar.Web.Data;
using BPRadar.Web.Features.Import;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Pages.Assessments;

public sealed class ImportModel(
    BPRadarDbContext dbContext,
    ImportSessionStore sessionStore,
    AssessmentImportService importService) : PageModel
{
    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public Guid ImportBatchId { get; set; }

    [BindProperty]
    public List<ColumnMappingInput> Mappings { get; set; } = [];

    public AssessmentImportHeading Assessment { get; private set; } = null!;
    public IReadOnlyList<string> Headers { get; private set; } = [];
    public ImportPreview? Preview { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        if (!await LoadAssessmentAsync(assessmentId, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        if (!await LoadAssessmentAsync(assessmentId, cancellationToken))
        {
            return NotFound();
        }

        if (Upload is null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a CSV or XLSX file.");
            return Page();
        }

        if (Upload.Length > TabularImportParser.MaximumFileSize)
        {
            ModelState.AddModelError(
                nameof(Upload),
                "The uploaded file exceeds the 5 MB limit.");
            return Page();
        }

        try
        {
            await using var stream = new MemoryStream(
                checked((int)Upload.Length));
            await Upload.CopyToAsync(stream, cancellationToken);
            var content = stream.ToArray();
            var table = TabularImportParser.Parse(content, Upload.FileName);
            var fileName = Path.GetFileName(Upload.FileName);
            var metadata = new ImportFileMetadata(
                fileName,
                content.LongLength,
                Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
            var suggestedMapping = ImportColumnMatcher.Match(table.Headers);
            var batch = new ImportBatch(
                Guid.NewGuid(),
                assessmentId,
                metadata,
                table,
                suggestedMapping);
            sessionStore.Add(batch);

            ImportBatchId = batch.Id;
            Headers = table.Headers;
            Mappings = ImportColumns.All
                .Select(column => new ColumnMappingInput
                {
                    LogicalName = column.Name,
                    SourceHeader = suggestedMapping[column.Name]
                })
                .ToList();
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(Upload), exception.Message);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        if (!await LoadAssessmentAsync(assessmentId, cancellationToken))
        {
            return NotFound();
        }

        if (!TryGetBatch(assessmentId, out var batch))
        {
            return Page();
        }

        Headers = batch.Table.Headers;
        ValidateMappings(batch);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var mapping = Mappings.ToDictionary(
            item => item.LogicalName,
            item => item.SourceHeader,
            StringComparer.Ordinal);
        Preview = await importService.PreviewAsync(
            batch,
            mapping,
            cancellationToken);
        sessionStore.SetPreview(batch.Id, Preview);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        if (!await LoadAssessmentAsync(assessmentId, cancellationToken))
        {
            return NotFound();
        }

        if (!TryGetBatch(assessmentId, out var batch) ||
            batch.Preview is null)
        {
            return Page();
        }

        var result = await importService.CommitAsync(batch, cancellationToken);
        sessionStore.Remove(batch.Id);
        TempData[nameof(IndexModel.ConfirmationMessage)] =
            $"Imported {result.UpsertedRows} result(s); " +
            $"{result.InvalidRows} row(s) were skipped.";
        return RedirectToPage("/Assessments/Index");
    }

    private bool TryGetBatch(int assessmentId, out ImportBatch batch)
    {
        if (ImportBatchId == Guid.Empty ||
            !sessionStore.TryGet(ImportBatchId, out batch!) ||
            batch.AssessmentId != assessmentId)
        {
            ModelState.AddModelError(
                string.Empty,
                "This import session has expired. Upload the file again.");
            batch = null!;
            return false;
        }

        return true;
    }

    private void ValidateMappings(ImportBatch batch)
    {
        var knownColumns = ImportColumns.All.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);
        var mappedColumns = Mappings.ToDictionary(
            mapping => mapping.LogicalName,
            StringComparer.Ordinal);

        foreach (var column in ImportColumns.All)
        {
            if (!mappedColumns.TryGetValue(column.Name, out var mapping))
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Mapping for {column.Name} is missing.");
                continue;
            }

            if (column.Required &&
                string.IsNullOrWhiteSpace(mapping.SourceHeader))
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Select a source column for {column.Name}.");
            }
            else if (!string.IsNullOrWhiteSpace(mapping.SourceHeader) &&
                !batch.Table.Headers.Contains(
                    mapping.SourceHeader,
                    StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"The selected source column for {column.Name} is invalid.");
            }
        }

        foreach (var mapping in Mappings)
        {
            if (!knownColumns.ContainsKey(mapping.LogicalName))
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Unknown logical column {mapping.LogicalName}.");
            }
        }
    }

    private async Task<bool> LoadAssessmentAsync(
        int assessmentId,
        CancellationToken cancellationToken)
    {
        Assessment = await dbContext.Assessments
            .AsNoTracking()
            .Where(item => item.Id == assessmentId)
            .Select(item => new AssessmentImportHeading(
                item.Id,
                item.Organization.Name,
                item.Framework.Name,
                item.Framework.Version,
                item.Label,
                item.SnapshotDate))
            .SingleOrDefaultAsync(cancellationToken)
            ?? null!;
        return Assessment is not null;
    }
}

public sealed class ColumnMappingInput
{
    [Required]
    public string LogicalName { get; set; } = string.Empty;

    public string? SourceHeader { get; set; }
}

public sealed record AssessmentImportHeading(
    int Id,
    string OrganizationName,
    string FrameworkName,
    string FrameworkVersion,
    string Label,
    DateTime SnapshotDate);
