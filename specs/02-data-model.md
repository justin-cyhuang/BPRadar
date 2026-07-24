# 02 — Data Model

Implemented with Entity Framework Core against SQLite. This document is the
source of truth for the schema; the actual EF Core model/migrations should
match it (update this spec first if the model needs to change).

## Entities

### Framework
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| Name | string | e.g. "ISO/IEC 27001", "Azure Well-Architected Framework" |
| Version | string | e.g. "2022", "5 pillars" |
| Description | string | short summary |
| SourceUrl | string? | optional canonical reference link |

### Domain
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| FrameworkId | int (FK → Framework) | |
| Code | string | e.g. "A.5", "Security" |
| Name | string | e.g. "Organizational controls", "Security" |
| SortOrder | int | for stable display ordering |

### Control
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| DomainId | int (FK → Domain) | |
| Code | string | e.g. "A.5.1", "WAF-REL-01" |
| Title | string | short title |
| Description | string | 1–2 sentence paraphrased purpose |
| GuidanceUrl | string? | optional reference link |
| SortOrder | int | |

### Organization
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| Name | string | the entity being assessed |
| Notes | string? | |

### Assessment
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| OrganizationId | int (FK → Organization) | |
| FrameworkId | int (FK → Framework) | one assessment targets one framework |
| Label | string | e.g. "2026 Q1 Security Review" |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### AssessmentResult
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| AssessmentId | int (FK → Assessment) | |
| ControlId | int (FK → Control) | |
| Status | enum `ComplianceStatus` | Compliant / Partial / NonCompliant / NotApplicable / NotAssessed |
| Score | decimal? | optional numeric score, e.g. 0–100 or 0–5 maturity |
| Notes | string? | assessor free-text notes |
| EvidenceUrl | string? | optional link/reference to evidence |
| Source | enum `ResultSource` | Manual / Import |
| UpdatedAt | DateTime | |

Uniqueness: one `AssessmentResult` per (`AssessmentId`, `ControlId`) pair —
re-entering a result for the same control in the same assessment updates the
existing row rather than duplicating it.

## Enums

```
enum ComplianceStatus { NotAssessed, Compliant, Partial, NonCompliant, NotApplicable }
enum ResultSource { Manual, Import }
```

## Relationships
```
Framework 1---* Domain 1---* Control
Organization 1---* Assessment *---1 Framework
Assessment 1---* AssessmentResult *---1 Control
```

## Derived metrics (computed, not stored)
- **Completion %** per Assessment = `(count of results where Status !=
  NotAssessed) / (count of Controls in the Framework)`
- **Compliance %** per Assessment = `(count of results where Status ==
  Compliant) / (count of assessed results, i.e. Status != NotAssessed)`
- **Gap list** per Assessment = all `AssessmentResult` rows where Status is
  `Partial` or `NonCompliant`, joined with Control/Domain for display.
- **Domain score** (used for radar chart axis per domain) = average of
  numeric mapping of Status within that domain for the assessment:
  `Compliant=1.0, Partial=0.5, NonCompliant=0.0` (NotApplicable and
  NotAssessed excluded from the average).

## Notes
- SQLite file path: configurable via `appsettings.json`
  (`ConnectionStrings:Default`), defaulting to a local `bpradar.db` file
  that is **not** committed to git (see `.gitignore` in `06-tech-stack.md`).
- Seed data (frameworks/domains/controls from `01-frameworks.md`) is loaded
  via an EF Core seeding step / a dedicated seed script, kept separate from
  user-entered `Organization`/`Assessment`/`AssessmentResult` data so it can
  be re-run safely without touching assessment data.
