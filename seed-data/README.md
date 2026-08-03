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
