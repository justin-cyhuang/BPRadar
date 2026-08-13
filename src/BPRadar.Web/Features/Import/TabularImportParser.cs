using System.Text;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualBasic.FileIO;

namespace BPRadar.Web.Features.Import;

public static class TabularImportParser
{
    public const long MaximumFileSize = 5 * 1024 * 1024;

    public static ImportTable Parse(byte[] content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (content.Length == 0)
        {
            throw new InvalidDataException("The uploaded file is empty.");
        }

        if (content.Length > MaximumFileSize)
        {
            throw new InvalidDataException("The uploaded file exceeds the 5 MB limit.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".csv")
        {
            return ParseCsv(content);
        }

        if (extension != ".xlsx")
        {
            throw new InvalidDataException("Only CSV and XLSX files are supported.");
        }

        try
        {
            return ParseXlsx(content);
        }
        catch (OpenXmlPackageException exception)
        {
            throw new InvalidDataException(
                "The XLSX workbook is invalid or corrupted.",
                exception);
        }
        catch (FileFormatException exception)
        {
            throw new InvalidDataException(
                "The XLSX workbook is invalid or corrupted.",
                exception);
        }
    }

    private static ImportTable ParseCsv(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var parser = new TextFieldParser(
            stream,
            Encoding.UTF8,
            detectEncoding: true,
            leaveOpen: false)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var headers = parser.ReadFields()
            ?? throw new InvalidDataException("The CSV file does not contain a header row.");
        ValidateHeaders(headers);

        var rows = new List<ImportTableRow>();
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException exception)
            {
                throw new InvalidDataException(
                    $"CSV row {rowNumber} is malformed.",
                    exception);
            }

            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new ImportTableRow(
                rowNumber,
                Pad(fields, headers.Length)));
        }

        return new ImportTable(headers, rows);
    }

    private static ImportTable ParseXlsx(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The XLSX workbook is invalid.");
        var styles = workbookPart.WorkbookStylesPart?.Stylesheet;
        var uses1904DateSystem =
            workbookPart.Workbook.WorkbookProperties?.Date1904?.Value ?? false;
        var sheet = workbookPart.Workbook.Sheets?
            .Elements<Sheet>()
            .FirstOrDefault()
            ?? throw new InvalidDataException("The XLSX workbook has no worksheets.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sourceRows = worksheetPart.Worksheet
            .GetFirstChild<SheetData>()?
            .Elements<Row>()
            .ToArray()
            ?? [];
        if (sourceRows.Length == 0)
        {
            throw new InvalidDataException("The first XLSX worksheet is empty.");
        }

        var sharedStrings = workbookPart.SharedStringTablePart?
            .SharedStringTable;
        var headerValues = ReadXlsxRow(
            sourceRows[0],
            sharedStrings,
            styles,
            uses1904DateSystem);
        ValidateHeaders(headerValues);

        var rows = new List<ImportTableRow>();
        foreach (var sourceRow in sourceRows.Skip(1))
        {
            var values = Pad(
                ReadXlsxRow(
                    sourceRow,
                    sharedStrings,
                    styles,
                    uses1904DateSystem),
                headerValues.Length);
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new ImportTableRow(
                checked((int)(sourceRow.RowIndex?.Value ?? (uint)(rows.Count + 2))),
                values));
        }

        return new ImportTable(headerValues, rows);
    }

    private static string[] ReadXlsxRow(
        Row row,
        SharedStringTable? sharedStrings,
        Stylesheet? styles,
        bool uses1904DateSystem)
    {
        var cells = row.Elements<Cell>().ToArray();
        if (cells.Length == 0)
        {
            return [];
        }

        var values = new string[cells.Max(cell => ColumnIndex(cell.CellReference?.Value)) + 1];
        foreach (var cell in cells)
        {
            values[ColumnIndex(cell.CellReference?.Value)] =
                ReadXlsxCell(
                    cell,
                    sharedStrings,
                    styles,
                    uses1904DateSystem);
        }

        return values;
    }

    private static string ReadXlsxCell(
        Cell cell,
        SharedStringTable? sharedStrings,
        Stylesheet? styles,
        bool uses1904DateSystem)
    {
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, out var sharedStringIndex))
        {
            return sharedStrings?.ElementAtOrDefault(sharedStringIndex)?.InnerText
                ?? string.Empty;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return cell.CellValue?.Text == "1" ? "TRUE" : "FALSE";
        }

        var rawValue = cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
        if (IsDateCell(cell, styles) &&
            double.TryParse(
                rawValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var serialDate))
        {
            if (uses1904DateSystem)
            {
                serialDate += 1462;
            }

            return DateTime.FromOADate(serialDate)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return rawValue;
    }

    private static bool IsDateCell(Cell cell, Stylesheet? styles)
    {
        if (cell.DataType?.Value == CellValues.Date)
        {
            return true;
        }

        var cellFormats = styles?.CellFormats?
            .Elements<CellFormat>()
            .ToArray();
        if (cell.StyleIndex is null ||
            cellFormats is null ||
            cell.StyleIndex.Value >= cellFormats.Length)
        {
            return false;
        }

        var format = cellFormats[(int)cell.StyleIndex.Value];
        var numberFormatId = format.NumberFormatId?.Value ?? 0;
        if (numberFormatId is >= 14 and <= 22 or >= 45 and <= 47)
        {
            return true;
        }

        var formatCode = styles?.NumberingFormats?
            .Elements<NumberingFormat>()
            .FirstOrDefault(item => item.NumberFormatId?.Value == numberFormatId)
            ?.FormatCode?.Value;
        if (string.IsNullOrWhiteSpace(formatCode))
        {
            return false;
        }

        var normalized = formatCode
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains('y') ||
            normalized.Contains('d');
    }

    private static int ColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        var index = 0;
        foreach (var character in cellReference.TakeWhile(char.IsLetter))
        {
            index = checked(index * 26 +
                (char.ToUpperInvariant(character) - 'A' + 1));
        }

        return Math.Max(0, index - 1);
    }

    private static string[] Pad(IReadOnlyList<string> values, int length)
    {
        var result = new string[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = index < values.Count
                ? values[index].Trim()
                : string.Empty;
        }

        return result;
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("The file does not contain a header row.");
        }

        var duplicate = headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .GroupBy(header => header.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"The file contains duplicate header '{duplicate.Key}'.");
        }
    }
}
