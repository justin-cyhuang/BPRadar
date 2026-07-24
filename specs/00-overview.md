# 00 — Overview

## Product name
**BPRadar** — Best-Practice Radar.

## Problem statement
Organizations are expected to align with multiple, overlapping best-practice
frameworks (security, service management, cloud architecture, etc.), but there
is no single, lightweight tool to:

1. Record what "good" looks like per framework (the controls/checklist items).
2. Capture how a specific organization currently measures up against each
   framework (manually assessed, or imported from existing assessment data).
3. See, at a glance, where the gaps are — both within one framework and
   **across** frameworks side by side.

BPRadar solves this by modeling frameworks as structured, comparable templates
and giving assessors a dashboard (including a radar/spider chart — the
"Radar" in the name) that shows compliance/maturity gaps per framework and in
comparison to one another.

## Primary persona
- **Assessor / GRC analyst**: reviews an organization against one or more
  frameworks, records results (manually or via import), and uses the
  dashboard to report gaps to stakeholders.

(Future personas — auditee self-service, executive read-only viewer — are
out of scope for MVP; see Non-goals.)

## Goals (MVP)
- Support at least these three framework templates out of the box:
  - **ISO/IEC 27001:2022** (Annex A controls)
  - **ISO/IEC 20000-1** (service management system clauses)
  - **Microsoft Azure Well-Architected Framework** (5 pillars)
- Allow new frameworks/templates to be added without code changes to the
  core data model (data-driven framework definitions).
- Allow assessment data to be entered manually via a UI, or imported in bulk
  from CSV/XLSX.
- Persist all data in a local SQLite database.
- Provide a dashboard that shows, per framework and organization:
  - overall completion / compliance %
  - gap list (non-compliant / partial controls) with drill-down
  - a radar chart comparing multiple frameworks (or multiple domains within
    a framework) for the same assessment/organization
- Support **baseline/target profiles** so assessors can define target
  compliance/score per framework and domain, then compare actual vs target.
- Support a recurring **company profile survey** (key items mapped to
  frameworks/domains/controls) so users can submit snapshots regularly and
  track enterprise transformation over time.
- Support **audit handoff reporting exports** in CSV and PDF formats.
- Ship as a single ASP.NET Core (.NET Core 10) web application, using only
  Microsoft-published packages (see `06-tech-stack.md`).

## Non-goals (MVP)
- **Authentication / authorization / multi-tenant access control** — single
  local user assumed for MVP.
- **Live/automated data pulls** from Microsoft APIs (e.g. Microsoft Defender
  for Cloud Secure Score, Azure Policy compliance) — data entry is manual or
  file-import only in MVP; this is called out as a future enhancement.
- **Advanced analytics** (forecasting/predictive scoring/ML) is out of scope.
- **Reproducing full standard text.** ISO 27001 and ISO 20000 are paid
  standards; BPRadar stores only control codes, short titles, and brief
  paraphrased descriptions sufficient for mapping and assessment — not the
  verbatim published standard text.
- **Multi-organization comparison UI** — the data model supports multiple
  `Organization` records, but the MVP UI can default to a single organization
  without building a full org-switching experience.

## High-level user flow
1. Assessor selects (or creates) an **Organization** and starts a new
   **Assessment** against a chosen **Framework** (e.g. ISO 27001:2022).
2. Assessor fills in results per **Control**, either:
   - manually, via a checklist-style form, or
   - by importing a CSV/XLSX file of prior assessment results.
3. Assessor repeats for other frameworks against the same Organization
   (e.g. also assess against Azure WAF).
4. Assessor fills the recurring **Company Profile Survey** (monthly/quarterly)
   to capture a compact transformation snapshot aligned with key template
   items.
5. Assessor opens the **Dashboard** to see completion/gap summaries per
   framework and a radar chart comparing the frameworks/domains side by side.
6. Assessor selects a **Baseline/Target Profile** to view target deltas, then
   exports CSV/PDF report artifacts for audit handoff.

## Related specs
- `01-frameworks.md` — framework/domain/control definitions in scope
- `02-data-model.md` — entity/relationship detail
- `03-manual-entry.md` — manual entry UX
- `04-import.md` — bulk import UX and rules
- `05-dashboard.md` — dashboard and radar chart detail
- `06-tech-stack.md` — technical architecture and coding ground rules
- `07-survey.md` — recurring company profile survey and transformation tracking
- `08-waf.md` — Azure Well-Architected Framework detailed control catalog and seed reference
