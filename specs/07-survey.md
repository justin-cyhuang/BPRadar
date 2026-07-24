# 07 — Company Profile Survey (Recurring Transformation Tracking)

This spec defines a recurring survey feature used to capture a compact
enterprise profile over time. It complements full framework assessments by
providing a lighter-weight cadence signal (monthly/quarterly) for tracking
transformation momentum.

## Purpose
- Capture key template-aligned items as short survey questions.
- Allow repeated submissions over time for the same organization.
- Produce trend/delta indicators that show transformation progress.

## Key concepts
- **Survey Template**: question set and cadence (Monthly/Quarterly/etc.).
- **Survey Question**: key item mapped to Framework/Domain/Control when
  possible for traceability.
- **Survey Submission**: one periodic snapshot completed for an organization.
- **Survey Response**: answer to one question in a submission.

## Survey template design rules
- Each template should include a balanced set of key items across target
  frameworks/domains (recommended 10–30 questions).
- Every question should map to at least one of:
  - Framework
  - Domain
  - Control
- Each question carries a default weight (`Weight = 1.0` unless overridden).
- Cadence is explicit on template (`Monthly`, `Quarterly`, etc.).

## Response model
- Primary answer type: `SurveyResponseLevel`
  (`VeryLow`, `Low`, `Medium`, `High`, `VeryHigh`, `NotApplicable`).
- Optional numeric score can be stored when needed for normalization.
- Optional response note is allowed.

## Submission workflow
1. User selects Organization + Survey Template.
2. User starts a new Survey Submission with snapshot date/label.
3. User answers all required questions and saves.
4. Final submit records `SubmittedAt`; submission becomes immutable for
   historical integrity (edit requires explicit clone/new submission flow).

## Cadence and regularity
- System computes due state from template cadence + last submitted snapshot:
  - On-time
  - Due soon
  - Overdue
- Dashboard highlights overdue survey templates per organization.

## Derived metrics
- **Profile score (0–100)**: weighted average from responses in a submission.
- **Transformation delta**: profile score difference vs previous submission
  for the same Organization + Survey Template.
- **Domain transformation delta**: latest domain-level score vs previous.

## Validation
- Required questions cannot be left unanswered at final submit.
- Snapshot date must be valid and not later than current UTC date.
- Submission must belong to existing Organization + active Survey Template.

## Tracing requirements
- Survey operations follow correlation ID rules in `06-tech-stack.md`.
- Minimum events:
  - `SurveySubmissionStarted`
  - `SurveySubmissionSaved`
  - `SurveySubmissionFinalized`
  - `SurveyTrendComputed`
- Include `OrganizationId`, `SurveyTemplateId`, `SurveySubmissionId`, and
  score/delta summary values in trace metadata.

## Relationship to other features
- Survey does **not** replace detailed framework assessments; it provides
  regular pulse tracking between deeper assessments/import cycles.
- Survey outputs are included in CSV/PDF reporting per `05-dashboard.md`.
