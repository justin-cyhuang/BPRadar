# 03 — Manual Entry Spec

Describes the UX/flow for an assessor manually recording assessment results
through the web UI (as opposed to bulk import — see `04-import.md`).

## Entry points
1. **New Assessment**: assessor picks an Organization (existing or creates a
   new one inline) and a Framework, gives the Assessment a Label, and clicks
   "Create". This creates the `Assessment` row and navigates to its checklist
   view with all Controls pre-populated at `Status = NotAssessed`.
2. **Continue Assessment**: from a list of existing Assessments, assessor
   opens one to continue filling it in.

## Checklist view
- Grouped by **Domain** (collapsible sections), each listing its **Controls**
  in `SortOrder`.
- Each Control row shows: Code, Title, Description (truncated with
  expand/"more"), and an inline edit control for:
  - **Status** — dropdown/segmented control:
    Not Assessed / Compliant / Partial / Non-Compliant / N/A
  - **Score** — optional numeric input (shown only if the framework/domain
    uses numeric scoring; otherwise hidden)
  - **Notes** — free-text, expandable textarea
  - **Evidence URL** — optional text/link input
- Changes save on blur / row-level "Save" (no separate global submit step
  needed) via an API call that upserts the corresponding `AssessmentResult`
  (unique on Assessment+Control per `02-data-model.md`), setting
  `Source = Manual` and `UpdatedAt = now`.
- A per-domain progress indicator (e.g. "12/20 assessed") and an overall
  Assessment progress indicator are shown at the top of the page, updating
  live as results are saved.

## Validation rules
- Status is required to consider a control "assessed" (default is
  `NotAssessed`, which does not count toward completion).
- Score, if provided, must be within the framework's configured numeric
  range (default 0–100 unless a domain overrides it) — validated client- and
  server-side.
- Notes/EvidenceUrl are optional free text; EvidenceUrl is validated as a
  well-formed URL if non-empty.

## Bulk manual actions (nice-to-have, not blocking MVP)
- "Mark all remaining controls in this domain as N/A" — useful when a whole
  domain is out of scope for a given organization.
- These are lower priority than the core per-control editing flow.

## Non-goals for this spec
- No multi-user concurrent editing/locking — single local user assumed.
- No workflow/approval states (e.g. draft vs. submitted) — a saved result is
  immediately reflected on the dashboard.
