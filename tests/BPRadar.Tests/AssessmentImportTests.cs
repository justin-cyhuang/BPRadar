using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using BPRadar.Web.Data;
using BPRadar.Web.Features.Assessments;
using BPRadar.Web.Features.Import;
using BPRadar.Web.Features.Surveys;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BPRadar.Tests;

[TestClass]
public sealed class AssessmentImportTests
{
    [TestMethod]
    public void Header_mapping_matches_exact_alias_and_fuzzy_names()
    {
        string[] headers =
        [
            "Organization Name",
            "Framework Code",
            "Framework Versoin",
            "Assessment",
            "Assessment Date",
            "Baseline Profile",
            "Control ID",
            "Domain",
            "Title",
            "Compliance Status",
            "Numeric Score",
            "Score Scale",
            "Evidence URL",
            "Comments",
            "External ID"
        ];

        var mapping = ImportColumnMatcher.Match(headers);

        Assert.AreEqual("Organization Name", mapping["OrganizationName"]);
        Assert.AreEqual("Framework Code", mapping["FrameworkCode"]);
        Assert.AreEqual("Framework Versoin", mapping["FrameworkVersion"]);
        Assert.AreEqual("Assessment Date", mapping["AssessmentSnapshotDate"]);
        Assert.AreEqual("Control ID", mapping["ControlCode"]);
        Assert.AreEqual("Compliance Status", mapping["Status"]);
        Assert.AreEqual("External ID", mapping["ExternalRecordId"]);
    }

    [TestMethod]
    [DataRow("Compliant", ComplianceStatus.Compliant)]
    [DataRow("yes", ComplianceStatus.Compliant)]
    [DataRow("No", ComplianceStatus.NonCompliant)]
    [DataRow("N/A", ComplianceStatus.NotApplicable)]
    [DataRow("Not Assessed", ComplianceStatus.NotAssessed)]
    public void Status_aliases_are_normalized(
        string source,
        ComplianceStatus expected)
    {
        Assert.IsTrue(AssessmentImportService.TryNormalizeStatus(source, out var status));
        Assert.AreEqual(expected, status);
    }

    [TestMethod]
    public void Scores_are_normalized_and_invalid_values_are_rejected()
    {
        Assert.IsTrue(AssessmentImportService.TryNormalizeScore(
            "4",
            "0-5",
            out var normalized,
            out var error));
        Assert.AreEqual(80m, normalized);
        Assert.IsNull(error);

        Assert.IsFalse(AssessmentImportService.TryNormalizeScore(
            "6",
            "0-5",
            out _,
            out error));
        StringAssert.Contains(error, "between 0 and 5");

        Assert.IsFalse(AssessmentImportService.TryNormalizeScore(
            "not-a-number",
            "0-100",
            out _,
            out error));
        StringAssert.Contains(error, "must be a number");
    }

    [TestMethod]
    public async Task Preview_rejects_unknown_control_invalid_status_score_and_future_date()
    {
        await using var database = await ImportTestDatabase.CreateAsync();
        var assessment = await database.CreateAssessmentAsync(
            "Azure Well-Architected Framework",
            "Validation Review",
            DateTime.UtcNow.Date);
        var tomorrow = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var csv = FullHeader + "\n" +
            Row("UNKNOWN", "RE", "Compliant", "90", "0-100", today) + "\n" +
            Row("RE:01", "RE", "Maybe", "90", "0-100", today) + "\n" +
            Row("RE:01", "RE", "Compliant", "101", "0-100", today) + "\n" +
            Row("RE:01", "RE", "Compliant", "90", "0-100", tomorrow);
        var batch = CreateBatch(assessment.Id, "validation.csv", csv);

        var preview = await database.Service.PreviewAsync(
            batch,
            batch.SuggestedMapping);

        Assert.AreEqual(0, preview.ValidRows);
        Assert.AreEqual(4, preview.InvalidRows);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "UnrecognizedControlCode",
                "InvalidStatus",
                "InvalidScore",
                "FutureSnapshotDate"
            },
            preview.Errors.Select(error => error.ReasonCode).ToArray());
    }

    [TestMethod]
    public async Task Confirm_import_commits_valid_rows_when_other_rows_are_invalid()
    {
        await using var database = await ImportTestDatabase.CreateAsync();
        var assessment = await database.CreateAssessmentAsync(
            "Azure Well-Architected Framework",
            "Partial Review",
            DateTime.UtcNow.Date);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var csv = FullHeader + "\n" +
            Row("RE:01", "RE", "Yes", "4", "0-5", today) + "\n" +
            Row("UNKNOWN", "RE", "Compliant", "90", "0-100", today);
        var batch = CreateBatch(assessment.Id, "partial.csv", csv);
        var preview = await database.Service.PreviewAsync(
            batch,
            batch.SuggestedMapping);
        batch = batch with { Preview = preview };

        var result = await database.Service.CommitAsync(batch);

        Assert.AreEqual(1, preview.ValidRows);
        Assert.AreEqual(1, preview.InvalidRows);
        Assert.AreEqual(1, result.UpsertedRows);
        var imported = await database.Context.AssessmentResults
            .SingleAsync(item =>
                item.AssessmentId == assessment.Id &&
                item.Control.Code == "RE:01");
        Assert.AreEqual(ComplianceStatus.Compliant, imported.Status);
        Assert.AreEqual(80m, imported.Score);
        Assert.AreEqual(ResultSource.Import, imported.Source);
        Assert.AreEqual("EXT-1", imported.ExternalRecordId);
    }

    [TestMethod]
    public async Task Duplicate_control_code_is_rejected_and_does_not_overwrite_first_row()
    {
        await using var database = await ImportTestDatabase.CreateAsync();
        var assessment = await database.CreateAssessmentAsync(
            "Azure Well-Architected Framework",
            "Partial Review",
            DateTime.UtcNow.Date);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var csv = FullHeader + "\n" +
            Row("RE:01", "RE", "Compliant", "90", "0-100", today) + "\n" +
            Row("RE:01", "RE", "NonCompliant", "10", "0-100", today);
        var batch = CreateBatch(assessment.Id, "partial.csv", csv);

        var preview = await database.Service.PreviewAsync(
            batch,
            batch.SuggestedMapping);
        batch = batch with { Preview = preview };
        var result = await database.Service.CommitAsync(batch);

        Assert.AreEqual(1, preview.ValidRows);
        Assert.AreEqual(1, preview.InvalidRows);
        Assert.AreEqual(1, preview.RowsToUpdate);
        Assert.AreEqual(
            "DuplicateControlCode",
            preview.Errors.Single().ReasonCode);
        Assert.AreEqual(3, preview.Errors.Single().RowNumber);
        Assert.AreEqual(1, result.UpsertedRows);
        var imported = await database.Context.AssessmentResults
            .SingleAsync(item =>
                item.AssessmentId == assessment.Id &&
                item.Control.Code == "RE:01");
        Assert.AreEqual(ComplianceStatus.Compliant, imported.Status);
        Assert.AreEqual(90m, imported.Score);
    }

    [TestMethod]
    [DataRow(
        "waf-import-sample.csv",
        "Azure Well-Architected Framework",
        "2026 Q3 Azure WAF Review",
        "2026-07-01")]
    [DataRow(
        "iso27001-import-sample.csv",
        "ISO/IEC 27001",
        "2026 ISMS Internal Review",
        "2026-08-01")]
    [DataRow(
        "iso20000-import-sample.csv",
        "ISO/IEC 20000-1",
        "2026 SMS Internal Review",
        "2026-08-01")]
    public async Task Checked_in_sample_imports_round_trip(
        string fileName,
        string frameworkName,
        string label,
        string snapshotDate)
    {
        await using var database = await ImportTestDatabase.CreateAsync();
        var assessment = await database.CreateAssessmentAsync(
            frameworkName,
            label,
            DateTime.Parse(snapshotDate));
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "seed-data",
            "samples",
            fileName);
        var content = await File.ReadAllBytesAsync(path);
        var table = TabularImportParser.Parse(content, fileName);
        var batch = new ImportBatch(
            Guid.NewGuid(),
            assessment.Id,
            new ImportFileMetadata(
                fileName,
                content.LongLength,
                Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()),
            table,
            ImportColumnMatcher.Match(table.Headers));

        var preview = await database.Service.PreviewAsync(
            batch,
            batch.SuggestedMapping);
        batch = batch with { Preview = preview };
        var result = await database.Service.CommitAsync(batch);

        Assert.AreEqual(10, preview.RowsRead);
        Assert.AreEqual(10, preview.ValidRows);
        Assert.AreEqual(0, preview.InvalidRows);
        Assert.AreEqual(10, result.UpsertedRows);
        Assert.AreEqual(
            10,
            await database.Context.AssessmentResults.CountAsync(item =>
                item.AssessmentId == assessment.Id &&
                item.Source == ResultSource.Import));
    }

    [TestMethod]
    public void Xlsx_parser_reads_the_first_worksheet()
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(
            stream,
            SpreadsheetDocumentType.Workbook,
            autoSave: true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(new CellFormats(
                new CellFormat(),
                new CellFormat
                {
                    NumberFormatId = 14,
                    ApplyNumberFormat = true
                }));
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var resultRow = InlineRow(2, "RE:01", "Yes");
            resultRow.Append(new Cell
            {
                CellReference = "C2",
                StyleIndex = 1,
                CellValue = new CellValue(
                    new DateTime(2026, 8, 1).ToOADate()
                        .ToString(CultureInfo.InvariantCulture))
            });
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                InlineRow(
                    1,
                    "Control Code",
                    "Compliance Status",
                    "Assessment Date"),
                resultRow));
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Results"
            });
        }

        var table = TabularImportParser.Parse(stream.ToArray(), "results.xlsx");

        CollectionAssert.AreEqual(
            new[] { "Control Code", "Compliance Status", "Assessment Date" },
            table.Headers.ToArray());
        Assert.HasCount(1, table.Rows);
        Assert.AreEqual("RE:01", table.Rows[0].Values[0]);
        Assert.AreEqual("Yes", table.Rows[0].Values[1]);
        Assert.AreEqual("2026-08-01", table.Rows[0].Values[2]);
    }

    [TestMethod]
    public void Parser_rejects_files_larger_than_five_megabytes()
    {
        var content = new byte[TabularImportParser.MaximumFileSize + 1];

        var exception = Assert.Throws<InvalidDataException>(
            () => TabularImportParser.Parse(content, "too-large.csv"));

        StringAssert.Contains(exception.Message, "5 MB");
    }

    [TestMethod]
    public void Parser_reports_corrupted_xlsx_as_invalid_data()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            TabularImportParser.Parse(
                Encoding.UTF8.GetBytes("not an Open XML package"),
                "corrupted.xlsx"));

        StringAssert.Contains(exception.Message, "invalid or corrupted");
    }

    private const string FullHeader =
        "OrganizationName,FrameworkCode,FrameworkVersion,AssessmentLabel," +
        "AssessmentSnapshotDate,BaselineProfileName,ControlCode,DomainCode," +
        "ControlTitle,Status,Score,ScoreScale,EvidenceUrl,Notes,ExternalRecordId";

    private static string Row(
        string controlCode,
        string domainCode,
        string status,
        string score,
        string scale,
        string snapshotDate) =>
        $"Contoso Ltd,AZURE_WAF,2026-07,{{LABEL}},{snapshotDate}," +
        $"2026 Internal Target,{controlCode},{domainCode},Control title," +
        $"{status},{score},{scale},,,EXT-1";

    private static ImportBatch CreateBatch(
        int assessmentId,
        string fileName,
        string csv)
    {
        var assessmentLabel = fileName == "validation.csv"
            ? "Validation Review"
            : "Partial Review";
        var content = Encoding.UTF8.GetBytes(
            csv.Replace("{LABEL}", assessmentLabel, StringComparison.Ordinal));
        var table = TabularImportParser.Parse(content, fileName);
        return new ImportBatch(
            Guid.NewGuid(),
            assessmentId,
            new ImportFileMetadata(fileName, content.LongLength, "test-hash"),
            table,
            ImportColumnMatcher.Match(table.Headers));
    }

    private static Row InlineRow(uint rowIndex, params string[] values)
    {
        var row = new Row { RowIndex = rowIndex };
        for (var index = 0; index < values.Length; index++)
        {
            row.Append(new Cell
            {
                CellReference = $"{(char)('A' + index)}{rowIndex}",
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(values[index]))
            });
        }

        return row;
    }

    private sealed class ImportTestDatabase(
        SqliteConnection connection,
        BPRadarDbContext context) : IAsyncDisposable
    {
        private readonly SqliteConnection connection = connection;

        public BPRadarDbContext Context { get; } = context;
        public AssessmentImportService Service { get; } =
            new(context, TimeProvider.System);

        public static async Task<ImportTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<BPRadarDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new BPRadarDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await DatabaseSeeder.SeedAsync(
                context,
                Path.Combine(AppContext.BaseDirectory, "seed-data"));
            return new ImportTestDatabase(connection, context);
        }

        public async Task<Assessment> CreateAssessmentAsync(
            string frameworkName,
            string label,
            DateTime snapshotDate)
        {
            var organization = new Organization { Name = "Contoso Ltd" };
            Context.Organizations.Add(organization);
            await Context.SaveChangesAsync();
            var framework = await Context.Frameworks.SingleAsync(
                item => item.Name == frameworkName);
            var created = await AssessmentService.CreateAsync(
                Context,
                new CreateAssessmentRequest(
                    organization.Id,
                    null,
                    framework.Id,
                    label,
                    snapshotDate));
            var assessment = await Context.Assessments.SingleAsync(
                item => item.Id == created.Assessment!.Id);
            var now = DateTime.UtcNow;
            var baseline = new BaselineProfile
            {
                Organization = organization,
                Name = "2026 Internal Target",
                CreatedAt = now,
                UpdatedAt = now
            };
            assessment.BaselineProfile = baseline;
            await Context.SaveChangesAsync();
            return assessment;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
