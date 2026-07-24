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

## Expected input shape
The importer expects one row per control result, with these logical columns
(header names are matched case-insensitively; exact header names are
finalized during implementation but conceptually):
| Column | Required | Notes |
|---|---|---|
| Control Code | yes | must match an existing `Control.Code` within the Assessment's Framework |
| Status | yes | one of: Compliant, Partial, Non-Compliant, N/A (case-insensitive; also accepts common synonyms like "Yes/No/Partial") |
| Score | no | numeric, optional |
| Notes | no | free text |
| Evidence URL | no | free text/URL |

## Import flow
1. Assessor opens an existing Assessment and chooses "Import results".
2. Uploads a CSV/XLSX file.
3. Server parses the header row and shows a **column-mapping UI**: for each
   expected logical column (Control Code, Status, Score, Notes, Evidence
   URL), the assessor picks which source column it corresponds to (defaults
   are pre-selected via case-insensitive/fuzzy header matching).
4. Assessor clicks "Preview" — server parses all rows using the chosen
   mapping and returns a preview table plus a validation summary:
   - rows that will be **created/updated** (matched Control Code)
   - rows with **unrecognized Control Code** (skipped, listed with row #)
   - rows with **invalid Status/Score** (skipped, listed with row # and reason)
5. Assessor clicks "Confirm import" — server upserts `AssessmentResult` rows
   for all valid rows (unique on Assessment+Control, per `02-data-model.md`),
   setting `Source = Import` and `UpdatedAt = now`. Rows already present are
   updated (import overwrites manual entries for the same control unless we
   later add a "don't overwrite manual" toggle — out of scope for MVP but
   noted as a possible enhancement).

## Validation rules
- File size limit (e.g. 5 MB) enforced server-side to avoid abuse.
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
