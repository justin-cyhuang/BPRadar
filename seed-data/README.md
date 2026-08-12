# Seed / sample data (pre-coding staging area)

This folder holds ready-to-use fixtures prepared **before** Phase 1 bootstrap
(`06-tech-stack.md`), so the WAF benchmark can be implemented without
re-deriving data from the specs.

Once the ASP.NET Core project exists, these files should move into their
final homes and this folder can be removed:

| File | Final destination (proposed) |
|---|---|
| `frameworks/azure-waf.json` | `src/BPRadar.Web/Data/Seed/azure-waf.json` (loaded by the EF Core seeding routine, `02-data-model.md`) |
| `frameworks/iso27001.json` | `src/BPRadar.Web/Data/Seed/iso27001.json` |
| `frameworks/iso20000.json` | `src/BPRadar.Web/Data/Seed/iso20000.json` |
| `control-keywords.json` | `src/BPRadar.Web/Data/Seed/control-keywords.json` |
| `control-keywords.schema.json` | `src/BPRadar.Web/Data/Seed/control-keywords.schema.json` |
| `control-keywords-authoring.md` | project documentation (offline refresh prompt/process) |
| `survey/waf-survey-template.json` | `src/BPRadar.Web/Features/Surveys/Seed/waf-survey-template.json` |
| `survey/iso27001-survey-template.json` | `src/BPRadar.Web/Features/Surveys/Seed/iso27001-survey-template.json` |
| `survey/iso20000-survey-template.json` | `src/BPRadar.Web/Features/Surveys/Seed/iso20000-survey-template.json` |
| `samples/waf-import-sample.csv` | `tests/BPRadar.Tests/Fixtures/` (import validation tests) and/or a "Download sample template" link in the Import UI (`04-import.md`) |
| `samples/iso27001-import-sample.csv` | same as above, ISO 27001 flavor |
| `samples/iso20000-import-sample.csv` | same as above, ISO 20000-1 flavor |

## Contents

- **`frameworks/azure-waf.json`** — Framework → Domain → Control seed fixture
  for the Azure Well-Architected Framework, matching the shape in
  `02-data-model.md` §"Seeding shape" and the full catalog in `08-waf.md` §3.
  Keyed on `FrameworkCode + ControlCode` for idempotent re-seeding.
- **`frameworks/iso27001.json`** — Framework → Domain → Control seed fixture
  for ISO/IEC 27001:2022 Annex A, all 93 controls across the 4 themes
  documented in `09-iso27001.md` §3. Same idempotent-seed key convention as
  the WAF fixture. Descriptions are original paraphrases, not standard text.
- **`frameworks/iso20000.json`** — Framework → Domain → Control seed fixture
  for ISO/IEC 20000-1:2018, 32 curated items across the 7 clause groups in
  `10-iso20000.md` §3. Includes a `_notes.warning` field flagging that the
  `OPS-xx` codes are BPRadar-curated and unverified against the paid standard
  — see `10-iso20000.md`'s confidence note before treating them as official.
- **`control-keywords.json`** — static Control Keyword fixture for every
  control in all three framework catalogs (59 WAF + 93 ISO 27001 + 32 ISO
  20000-1 = 184 controls). Each entry has 2–5 lowercase trigger phrases
  intended for the two-stage extraction/fuzzy-matching pipeline in
  `11-issue-matching.md`. The future loader resolves `ControlId` from
  `frameworkCode + controlCode` and upserts keywords using
  `frameworkCode + controlCode + normalized keyword` as its idempotency key.
- **`control-keywords.schema.json`** — JSON Schema for the Control Keyword
  fixture. It fixes the current seed contract at `schemaVersion: 1` and
  enforces the 2–5 keyword range and normalized phrase shape.
- **`control-keywords-authoring.md`** — exact reusable authoring prompt,
  deterministic catalog-to-prompt input command, output contract, and human
  discrimination review checklist.
- **`survey/waf-survey-template.json`** — a draft recurring Company Profile
  Survey template (`07-survey.md`) with 20 questions spanning all 5 WAF
  pillars, each mapped to a real `ControlCode`.
- **`survey/iso27001-survey-template.json`** — a draft quarterly pulse survey
  with 16 questions spanning all 4 Annex A themes, mapped to real
  `ControlCode`s from `iso27001.json`.
- **`survey/iso20000-survey-template.json`** — a draft quarterly pulse survey
  with 13 questions spanning all 7 clause groups (weighted toward the
  Operation domain), mapped to `ControlCode`s from `iso20000.json`.
- **`samples/waf-import-sample.csv`** — a filled-in example import file per
  `04-import.md`'s column layout, using real WAF control codes and a mix of
  status aliases/score scales to exercise the normalization rules.
- **`samples/iso27001-import-sample.csv`** — same pattern as the WAF sample,
  using real ISO 27001:2022 Annex A control codes (A.5/A.6/A.7/A.8).
- **`samples/iso20000-import-sample.csv`** — same pattern as the WAF sample,
  using ISO 20000-1 clause codes (C4–C10, including curated `OPS-xx` items).

## Control Keyword authoring and refresh process

Control Keywords are authored once and committed; they are never generated at
runtime. To refresh them after a catalog change:

1. Follow `control-keywords-authoring.md` to export every changed control's
   `code`, `title`, and paraphrased `description`, then run the documented
   prompt to generate 2–5 candidate phrases per control.
2. Review the suggestions against neighboring controls. Prefer phrases that
   identify the control's distinct failure mode (for example, `restore test
   failed` for backup) over generic words such as `process`, `risk`,
   `security`, `management`, or `failure` on their own.
3. Normalize phrases to lowercase, trim whitespace, remove duplicates within a
   control, and keep each phrase short enough for fuzzy/word-overlap matching.
   Overlap between frameworks is intentional when controls are genuine
   equivalents; overlap between unrelated controls should be removed.
4. Add/remove control entries so the fixture exactly matches all three
   framework catalogs, update `authoring.lastReviewed`, then run:

   ```powershell
   & .\seed-data\tests\control-keywords.tests.ps1
   ```

   The validation checks the JSON Schema, exact cross-catalog coverage,
   one entry per control, the 184-control baseline, normalized uniqueness, and
   the required 2–5 keywords per control. Update the expected baseline count
   when a catalog revision legitimately changes it.
