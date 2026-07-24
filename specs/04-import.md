# 04 — Import Spec (CSV / XLSX)

Describes bulk-importing assessment results into an existing `Assessment`
from a CSV or XLSX file, as an alternative/complement to manual entry.

## Supported file types
- **CSV** — parsed using built-in .NET APIs (e.g. `System.IO` + simple
  RFC 4180-aware splitting, or `Microsoft.VisualBasic.FileIO.TextFieldParser`
  which ships with .NET and is Microsoft-published). No third-party CSV
  package is used, per the Microsoft-only-packages ground rule.
- **XLSX** — parsed using **DocumentFormat.OpenXml** (Microsoft's Open XML
  SDK, a Microsoft-published NuGet package), reading the first worksheet by
  default (worksheet selection can be added later if needed).

## Import template definition (v1, for review)

### File layout
- CSV and XLSX use the same flat tabular layout (one row per control result).
- For XLSX, the first worksheet is used in MVP and must contain the table.
- Header matching is case-insensitive.

### Assessment-level metadata columns (repeated on each row)
| Column | Required | Notes |
|---|---|---|
| OrganizationName | yes | maps to `Organization.Name` |
| FrameworkCode | yes | one of: `ISO27001_2022`, `ISO20000_1`, `AZURE_WAF` |
| FrameworkVersion | no | optional cross-check against framework version in system |
| AssessmentLabel | yes | maps to `Assessment.Label` |
| AssessmentSnapshotDate | yes | business snapshot date (`yyyy-MM-dd`) |
| BaselineProfileName | no | optional link to baseline profile if present |

### Control-level benchmark columns
| Column | Required | Notes |
|---|---|---|
| ControlCode | yes | must match existing `Control.Code` in selected framework |
| DomainCode | no | optional cross-check; if present must match the control's domain |
| ControlTitle | no | optional informational cross-check only |
| Status | yes | normalized to `ComplianceStatus` |
| Score | no | numeric value (optional) |
| ScoreScale | no | `0-100` (default) or `0-5`; used to normalize score |
| EvidenceUrl | no | optional URL |
| Notes | no | optional free text |
| ExternalRecordId | no | optional source ID for traceability |

### Allowed values and normalization
- `FrameworkCode` mapping:
  - `ISO27001_2022` -> ISO/IEC 27001:2022
  - `ISO20000_1` -> ISO/IEC 20000-1
  - `AZURE_WAF` -> Azure Well-Architected Framework
- `Status` accepted values (case-insensitive):
  - Canonical: `Compliant`, `Partial`, `NonCompliant`, `NotApplicable`, `NotAssessed`
  - Aliases:
    - `Yes` -> `Compliant`
    - `No` -> `NonCompliant`
    - `N/A` -> `NotApplicable`
    - `Not Assessed` -> `NotAssessed`
- `Score` normalization:
  - if `ScoreScale=0-5`, normalize to 0-100 by `(score / 5) * 100`
  - if `ScoreScale=0-100` or empty, value is treated as 0-100

### Alignment rules to benchmark standards
- `FrameworkCode + ControlCode` is the primary standards alignment key.
- `ControlCode` must exist in the seeded control catalog for that framework.
- If `DomainCode` is provided, it must match the domain owning that control.
- If `FrameworkVersion` is provided and conflicts with system data, the row is
  flagged invalid (not silently corrected).
- Rows with unknown control references are rejected with explicit row-level
  errors in preview.

## Example rows (Azure WAF)

Real `ControlCode` values must come from the seeded control catalog for the
target framework — for Azure WAF that is the 59 codes in `08-waf.md` §3
(e.g. `RE:01`, `SE:05`, `CO:07`), never placeholders like `RE-01` or
`WAF-REL-01`. A ready-to-use example file with a full row set (including
`Yes`/`No`/`N/A` status aliases and both `0-100`/`0-5` score scales) is
checked in at `seed-data/samples/waf-import-sample.csv`. Header + first two
rows for reference:

```csv
OrganizationName,FrameworkCode,FrameworkVersion,AssessmentLabel,AssessmentSnapshotDate,BaselineProfileName,ControlCode,DomainCode,ControlTitle,Status,Score,ScoreScale,EvidenceUrl,Notes,ExternalRecordId
Contoso Ltd,AZURE_WAF,2026-07,2026 Q3 Azure WAF Review,2026-07-01,2026 Internal Target,RE:01,RE,Simplicity & efficiency,Compliant,90,0-100,https://contoso.example/evidence/re01,Design reviewed and simplified last quarter,EXT-RE-001
Contoso Ltd,AZURE_WAF,2026-07,2026 Q3 Azure WAF Review,2026-07-01,2026 Internal Target,RE:04,RE,Reliability targets,Yes,4,0-5,https://contoso.example/evidence/re04,SLO/RTO/RPO documented for tier-1 flows,EXT-RE-004
```

## Import flow
1. Assessor opens an existing Assessment and chooses "Import results".
2. Uploads a CSV/XLSX file.
3. Server parses the header row and shows a **column-mapping UI**: for each
   expected logical column (assessment metadata + control-level benchmark
   fields), the assessor picks which source column it corresponds to
   (defaults are pre-selected via case-insensitive/fuzzy header matching).
4. Assessor clicks "Preview" — server parses all rows using the chosen
   mapping and returns a preview table plus a validation summary, including:
   - assessment metadata consistency checks (Organization/Framework/Label/Date)
   - rows that will be **created/updated** (matched Control Code)
   - rows with **unrecognized Control Code** (skipped, listed with row #)
   - rows with **invalid Status/Score** (skipped, listed with row # and reason)
5. Assessor clicks "Confirm import" — server upserts `AssessmentResult` rows
   for all valid rows (unique on Assessment+Control, per `02-data-model.md`),
   setting `Source = Import` and `UpdatedAt = now`. Rows already present are
   updated (import overwrites manual entries for the same control unless we
   later add a "don't overwrite manual" toggle — out of scope for MVP but
   noted as a possible enhancement).

## Import tracing requirements
- Every import flow must run under a correlation ID and an `ImportBatchId`
  (GUID) so preview/confirm/error events can be tied together in logs.
- At minimum, trace these events:
  - `ImportPreviewStarted`
  - `ImportPreviewCompleted`
  - `ImportCommitStarted`
  - `ImportCommitCompleted`
  - `ImportCommitFailed`
- For each event, include:
  - correlation ID, `ImportBatchId`, `AssessmentId`, file metadata
    (name/size/hash), and row counts (read/valid/invalid/upserted).
- Row-level validation errors should be traced with row number and reason code,
  but without logging full free-text cell content.

## Validation rules
- File size limit (e.g. 5 MB) enforced server-side to avoid abuse.
- `OrganizationName`, `FrameworkCode`, `AssessmentLabel`,
  `AssessmentSnapshotDate`, `ControlCode`, and `Status` are required.
- `AssessmentSnapshotDate` must parse as a valid date and must not be in the
  future (UTC).
- Control Code must belong to a Control within the target Assessment's
  Framework — cross-framework codes are rejected as "unrecognized".
- Status values are normalized against the `ComplianceStatus` enum; anything
  unrecognized is rejected for that row (not defaulted silently).
- Score, if present, must parse as a number and fall within the configured
  valid range; invalid values are rejected for that row (with reason shown),
  not silently dropped.
- Partial success is allowed: valid rows import even if other rows in the
  same file are rejected, as long as the assessor confirms after reviewing
  the preview/validation summary.

## Non-goals for this spec
- No support for importing new Frameworks/Domains/Controls via this same
  pipeline in MVP — control taxonomy import is a possible future extension;
  this import only records **results** against existing controls.
- No scheduled/automatic re-import — manual, one-shot upload only.
