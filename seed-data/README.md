# Seed / sample data (pre-coding staging area)

This folder holds ready-to-use fixtures prepared **before** Phase 1 bootstrap
(`06-tech-stack.md`), so the WAF benchmark can be implemented without
re-deriving data from the specs.

Once the ASP.NET Core project exists, these files should move into their
final homes and this folder can be removed:

| File | Final destination (proposed) |
|---|---|
| `frameworks/azure-waf.json` | `src/BPRadar.Web/Data/Seed/azure-waf.json` (loaded by the EF Core seeding routine, `02-data-model.md`) |
| `survey/waf-survey-template.json` | `src/BPRadar.Web/Features/Surveys/Seed/waf-survey-template.json` |
| `samples/waf-import-sample.csv` | `tests/BPRadar.Tests/Fixtures/` (import validation tests) and/or a "Download sample template" link in the Import UI (`04-import.md`) |

## Contents

- **`frameworks/azure-waf.json`** — Framework → Domain → Control seed fixture
  for the Azure Well-Architected Framework, matching the shape in
  `02-data-model.md` §"Seeding shape" and the full catalog in `08-waf.md` §3.
  Keyed on `FrameworkCode + ControlCode` for idempotent re-seeding.
- **`survey/waf-survey-template.json`** — a draft recurring Company Profile
  Survey template (`07-survey.md`) with 20 questions spanning all 5 WAF
  pillars, each mapped to a real `ControlCode`.
- **`samples/waf-import-sample.csv`** — a filled-in example import file per
  `04-import.md`'s column layout, using real WAF control codes and a mix of
  status aliases/score scales to exercise the normalization rules.
