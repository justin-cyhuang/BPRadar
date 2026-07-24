# 05 — Dashboard Spec

Describes the consolidated dashboard: the primary "so what" view of BPRadar,
showing compliance/gap status per framework and a cross-framework radar
comparison.

## Scope selector
- Top of the dashboard: pick an **Organization**, then one or more
  **Assessments** for that organization to include in the view (defaulting
  to the most recent Assessment per Framework for that Organization).

## Overview cards (per selected Assessment)
For each included Assessment, show a summary card with:
- Framework name/version
- **Completion %** (controls assessed / total controls) — see formula in
  `02-data-model.md`
- **Compliance %** (compliant / assessed) — see formula in `02-data-model.md`
- **Gap count** — number of controls at Partial or Non-Compliant
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
- **Rendering**: custom SVG component rendered server-side/in Razor (no
  third-party charting package, per the Microsoft-only-packages ground
  rule) — plots N axes evenly spaced around a circle, draws gridlines at
  25/50/75/100%, and plots each series as a closed polygon with a legend.

## Non-goals for this spec
- No historical trend-over-time charts (would require multiple Assessments
  over time per Framework, plotted as a line chart) — flagged as a future
  enhancement building on the same data model.
- No PDF/print export in MVP — screen-only dashboard.
- No live data refresh from external APIs — dashboard reflects whatever is
  currently stored via manual entry or import.
