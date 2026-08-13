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
| Code | string | e.g. "A.5.1", "RE:01" (real WAF codes — see `08-waf.md`) |
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
| BaselineProfileId | int? (FK → BaselineProfile) | optional target profile used for this assessment/report view |
| Label | string | e.g. "2026 Q1 Security Review" |
| SnapshotDate | DateTime | business-effective date for the assessment/import snapshot |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### BaselineProfile
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| OrganizationId | int (FK → Organization) | profile is scoped to an organization |
| Name | string | e.g. "2026 Internal Target" |
| Description | string? | |
| IsDefault | bool | optional default profile for organization |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### BaselineTarget
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| BaselineProfileId | int (FK → BaselineProfile) | |
| FrameworkId | int (FK → Framework) | |
| DomainId | int? (FK → Domain) | null = framework-level target, set = domain-level target |
| TargetCompliancePercent | decimal? | target compliance %, e.g. 90.00 |
| TargetScore | decimal? | optional target score on the normalized chart scale |
| Notes | string? | |

### SurveyTemplate
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| Name | string | e.g. "Enterprise Transformation Pulse" |
| FrameworkId | int? (FK → Framework) | null = cross-framework template |
| Description | string? | |
| Cadence | enum `SurveyCadence` | Monthly / Quarterly / SemiAnnual / Annual |
| IsActive | bool | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### SurveyQuestion
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| SurveyTemplateId | int (FK → SurveyTemplate) | |
| Code | string | e.g. `SVY-SEC-01` |
| Prompt | string | survey question text |
| FrameworkId | int? (FK → Framework) | optional explicit mapping |
| DomainId | int? (FK → Domain) | optional mapping for domain rollup |
| ControlId | int? (FK → Control) | optional mapping for control traceability |
| Weight | decimal | default 1.0; used in weighted score |
| SortOrder | int | |
| IsRequired | bool | |

### SurveySubmission
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| OrganizationId | int (FK → Organization) | |
| SurveyTemplateId | int (FK → SurveyTemplate) | |
| Label | string | e.g. "2026 Q3 Transformation Pulse" |
| SnapshotDate | DateTime | business-effective snapshot date |
| SubmittedAt | DateTime | when submission is finalized |
| Notes | string? | optional summary notes |

### SurveyResponse
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| SurveySubmissionId | int (FK → SurveySubmission) | |
| SurveyQuestionId | int (FK → SurveyQuestion) | |
| ResponseLevel | enum `SurveyResponseLevel` | VeryLow / Low / Medium / High / VeryHigh / NotApplicable |
| Score | decimal? | optional numeric override on normalized scale |
| Notes | string? | optional response note |

### AssessmentResult
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| AssessmentId | int (FK → Assessment) | |
| ControlId | int (FK → Control) | |
| Status | enum `ComplianceStatus` | Compliant / Partial / NonCompliant / NotApplicable / NotAssessed |
| Score | decimal? | optional internal/import reference score, e.g. 0–100 or 0–5 maturity; not shown in assessor-facing UI until a user guidance rubric is defined |
| Notes | string? | assessor free-text notes |
| EvidenceUrl | string? | optional link/reference to evidence |
| ExternalRecordId | string? | optional source-system identifier from an import |
| Source | enum `ResultSource` | Manual / Import |
| UpdatedAt | DateTime | |

Uniqueness: one `AssessmentResult` per (`AssessmentId`, `ControlId`) pair —
re-entering a result for the same control in the same assessment updates the
existing row rather than duplicating it.

Uniqueness: one `BaselineTarget` per (`BaselineProfileId`, `FrameworkId`,
`DomainId`) tuple. (`DomainId = null` represents the framework-level target.)

Uniqueness: one `SurveyResponse` per (`SurveySubmissionId`, `SurveyQuestionId`)
pair.

### Issue
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| OrganizationId | int (FK → Organization) | scoped to org, not one framework — see `11-issue-matching.md` |
| Title | string | short summary |
| Description | string | free-text problem description |
| RootCause | string | free-text root cause, sourced from an external RCA process |
| MatchingStatus | enum `IssueMatchingStatus` | Pending / Matched / Failed |
| MatchingError | string? | error/timeout detail when `Failed` |
| CreatedAt | DateTime | |
| MatchedAt | DateTime? | when matching last completed |

### ControlKeyword
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| ControlId | int (FK → Control) | |
| Keyword | string | short trigger phrase used as a match target — see `11-issue-matching.md` |

### ViolationMatch
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| IssueId | int (FK → Issue) | |
| ControlId | int (FK → Control) | |
| MatchedKeywords | string | LLM-extracted keywords that matched this Control's ControlKeywords |
| MatchScore | decimal | fuzzy/word-overlap similarity score |
| IsSelfAssessmentDiscrepancy | bool | true when Self-Reported State claimed compliance but this match implies a violation |
| ReviewStatus | enum `ViolationMatchReviewStatus` | Open / Confirmed / Dismissed |
| CreatedAt | DateTime | |

Uniqueness: one `ViolationMatch` per (`IssueId`, `ControlId`) pair.

## Enums

```
enum ComplianceStatus { NotAssessed, Compliant, Partial, NonCompliant, NotApplicable }
enum ResultSource { Manual, Import }
enum SurveyCadence { Monthly, Quarterly, SemiAnnual, Annual }
enum SurveyResponseLevel { VeryLow, Low, Medium, High, VeryHigh, NotApplicable }
enum IssueMatchingStatus { Pending, Matched, Failed }
enum ViolationMatchReviewStatus { Open, Confirmed, Dismissed }
```

## Relationships
```
Framework 1---* Domain 1---* Control
Framework 1---* SurveyTemplate (optional binding)
SurveyTemplate 1---* SurveyQuestion
Organization 1---* SurveySubmission *---1 SurveyTemplate
SurveySubmission 1---* SurveyResponse *---1 SurveyQuestion
Organization 1---* BaselineProfile 1---* BaselineTarget
Framework 1---* BaselineTarget
Domain 1---* BaselineTarget (optional link)
Organization 1---* Assessment *---1 Framework
Assessment *---0..1 BaselineProfile
Assessment 1---* AssessmentResult *---1 Control
Organization 1---* Issue
Control 1---* ControlKeyword
Issue 1---* ViolationMatch *---1 Control
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
- **Framework target delta** = `Actual Compliance % - TargetCompliancePercent`
  (when framework-level target exists in selected BaselineProfile).
- **Domain target delta** = `Actual Domain score - TargetScore` (when
  domain-level target exists in selected BaselineProfile).
- **Survey profile score** per SurveySubmission = weighted average of mapped
  question scores (`Weight` from `SurveyQuestion`), normalized to 0–100.
- **Survey transformation delta** = current SurveySubmission profile score
  minus the prior submission score for the same Organization+SurveyTemplate.
- **Survey domain transformation delta** = per-domain score delta between the
  latest and previous submission (for questions mapped to the domain).

## Notes
- SQLite file path: configurable via `appsettings.json`
  (`ConnectionStrings:Default`), defaulting to a local `bpradar.db` file
  that is **not** committed to git (see `.gitignore` in `06-tech-stack.md`).
- Seed data (frameworks/domains/controls from `01-frameworks.md`) is loaded
  via an EF Core seeding step / a dedicated seed script, kept separate from
  user-entered `Organization`/`Assessment`/`AssessmentResult` data so it can
  be re-run safely without touching assessment data.
- Baseline profiles/targets are user-authored configuration and are not part
  of immutable framework seed data.
- Survey templates/questions can be seeded as defaults, but regular
  SurveySubmission/SurveyResponse records are operational time-series data.
