# 05 — Dashboard Spec

Describes the consolidated dashboard: the primary "so what" view of BPRadar,
showing compliance/gap status per framework and a cross-framework radar
comparison.

## Scope selector
- Top of the dashboard: pick an **Organization**, then one or more
  **Assessments** for that organization to include in the view (defaulting
  to the most recent Assessment per Framework for that Organization).
- Optional selector: pick a **Baseline/Target Profile** for the selected
  Organization. When selected, cards/charts/tables show actual-vs-target
  deltas.
- Optional selector: pick a **Survey Template** and date range to show
  recurring profile submissions and transformation deltas.

## Overview cards (per selected Assessment)
For each included Assessment, show a summary card with:
- Framework name/version
- **Completion %** (controls assessed / total controls) — see formula in
  `02-data-model.md`
- **Compliance %** (compliant / assessed) — see formula in `02-data-model.md`
- **Gap count** — number of controls at Partial or Non-Compliant
- **Target %** (if baseline selected) — framework-level target compliance %
- **Delta vs target** (if baseline selected) — actual minus target
- Last updated timestamp

## Gap drill-down
- Below the overview cards, a filterable/sortable table of all gap items
  (Status = Partial or Non-Compliant) across the selected Assessments:
  Framework, Domain, Control Code, Title, Status, Score, Notes.
- Filters: by Framework, by Domain, by Status (Partial vs Non-Compliant).
- Clicking a row deep-links to that control's entry in the manual entry view
  (`03-manual-entry.md`) for quick remediation/follow-up editing.

## Radar / spider comparison chart
- **Axes**: one axis per Domain (when comparing within a single framework)
  or one axis per Framework (when comparing across the selected
  frameworks/assessments) — the spec supports both modes; implementation
  starts with **cross-framework comparison** (axis = Framework) as the
  primary MVP mode, since that is the main "map best practices across
  benchmarks" scenario.
- **Value per axis** = the Domain score / overall Framework score formula
  from `02-data-model.md` (0.0–1.0 scale, or 0–100 if scaled for display).
- **Series** = one line/shape per Assessment selected (e.g. comparing the
  same Organization's ISO 27001 assessment vs. its Azure WAF assessment on
  a normalized 0–100 scale), or one shape per Organization if comparing
  organizations against the same framework (future enhancement).
- When a baseline profile is selected, render an additional **Target** series
  as a reference polygon (framework-level or domain-level targets depending on
  chart mode), visually distinct from actual assessment polygons.
- **Rendering**: custom SVG component rendered server-side/in Razor (no
  third-party charting package, per the Microsoft-only-packages ground
  rule) — plots N axes evenly spaced around a circle, draws gridlines at
  25/50/75/100%, and plots each series as a closed polygon with a legend.

## Baseline / target profile management
- Provide a simple profile management view for the selected Organization:
  - Create/edit/delete Baseline Profiles.
  - Mark one profile as default.
  - Set framework-level targets (compliance %) and optional domain-level
    targets (score/percent) per framework.
- Validation:
  - Percent targets must be 0–100.
  - A domain-level target requires the domain to belong to the chosen
    framework.
  - Duplicate targets for the same profile/framework/domain are not allowed.

## Company profile survey tracking
- Show a recurring survey panel for the selected Organization + Survey Template:
  - latest profile score (0–100)
  - delta vs previous submission
  - submission cadence status (on-time / overdue based on template cadence)
- Show a submission history table:
  - snapshot date, profile score, delta vs previous, key notes
  - quick action to open and review a submission
- Show transformation trend visualization:
  - simple line chart (custom SVG) of profile score by snapshot date
  - optional domain-level delta table (latest vs previous)

## Export / reporting (audit handoff)
- From the dashboard/report view, support:
  - **CSV export** for tabular audit evidence:
    - summary by framework (completion/compliance/target/delta)
    - gap list with domain/control/status/notes
    - survey submission history (snapshot date, score, delta)
    - survey domain deltas (if requested)
  - **PDF export** for audit handoff:
    - report header (organization, selected assessments, generated UTC time)
    - summary cards
    - radar chart snapshot
    - survey transformation summary (latest score/delta + trend snapshot)
    - gap table (paginated/truncated with continuation note if needed)
- Export scope follows current filters (organization, assessments, baseline
  profile, framework/domain/status filters).
- Export traceability:
  - include correlation ID and export timestamp in report metadata/footer.

## Non-goals for this spec
- No live data refresh from external APIs — dashboard reflects whatever is
  currently stored via manual entry or import.
- No predictive/forecast analytics on transformation data in MVP (descriptive
  trend/delta only).
