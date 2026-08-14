# 11 — Issue-to-Best-Practice-Violation Matching

This spec defines a feature that maps manually-entered operational **Issues**
(with an already-known **Root Cause**) back to candidate **Control**
violations, using LLM-assisted keyword extraction plus deterministic
keyword matching — and compares the result against each Control's
**Self-Reported State** (Survey/Assessment) to flag cases where an admin's
own records say "compliant" but the Issue shows otherwise.

See `CONTEXT.md` for full definitions of the terms used here (Issue, Root
Cause, Control Keyword, Violation Match, Self-Reported State, Observed State,
Self-Assessment Discrepancy). See `docs/adr/0001-llm-based-issue-matching.md`
for why this feature calls an external LLM despite `06-tech-stack.md`'s
Microsoft-only/no-external-services rule — that exception applies **only**
to this feature's keyword extraction step.

## Purpose
- Let an admin manually record an operational Issue (already root-caused by
  an external process) in BPRadar.
- Automatically surface which Controls (across **all** frameworks — WAF,
  ISO 27001, ISO 20000) that Issue's Root Cause plausibly violates.
- Compare that Observed State against the Control's existing Self-Reported
  State (from Survey responses or Assessment results) and prioritize cases
  where the two disagree.
- Give the admin static Control guidance as a starting-point suggestion, and
  let them confirm or dismiss each candidate match.

## Scope boundaries
- **No external ticketing integration in the sense of a live pull.** BPRadar
  never polls or reads from an incident/ticketing system. Issues are created
  through the existing `POST /api/organizations/{organizationId}/issues`
  endpoint — an admin typing into the UI and an external system pushing a
  request to that same endpoint are the same code path. Whether an external
  system is trusted to do that push is governed by `06-tech-stack.md`'s
  optional, config-gated API-key check (off by default) — not a bespoke
  integration built for any one ticketing tool.
- **No root cause analysis in BPRadar.** Root Cause is a field the admin
  copies in from an external RCA process/tool; BPRadar never derives it.
- **No write-back.** Confirming or dismissing a Violation Match never
  changes the underlying `AssessmentResult` or `SurveyResponse` data —
  matches are tracked independently.
- **No LLM-generated remediation text in this version.** Suggestions are the
  Control's existing `Description`/`GuidanceUrl` only. LLM-tailored
  remediation text is a wishlist item for a future version.
- **No dashboard integration in this version.** This feature has its own
  dedicated page; `05-dashboard.md` is unchanged.

## Entities (additions to `02-data-model.md`)

### Issue
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| OrganizationId | int (FK → Organization) | Issue is scoped to an org, not one framework |
| Title | string | short summary |
| Description | string | free-text problem description |
| RootCause | string | free-text root cause, sourced from an external process |
| MatchingStatus | enum `IssueMatchingStatus` | Pending / Matched / Failed |
| MatchingError | string? | error/timeout detail when `Failed`, shown to admin |
| CreatedAt | DateTime | |
| MatchedAt | DateTime? | when matching last completed (`Matched` or `Failed`) |

### ControlKeyword
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| ControlId | int (FK → Control) | |
| Keyword | string | short trigger phrase, e.g. "backup", "data loss" |

Seed data: keywords are generated once, offline, via a one-time LLM-assisted
authoring script (per control Title/Description), then reviewed/edited by
hand and committed as static seed data alongside the Framework/Domain/Control
seed data — not regenerated at runtime. Every existing Control (WAF 59 +
ISO 27001 93 + ISO 20000 32 ≈ 184) should have 2–5 seeded keywords.

### ViolationMatch
| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| IssueId | int (FK → Issue) | |
| ControlId | int (FK → Control) | |
| MatchedKeywords | string | comma/JSON list of LLM-extracted keywords that matched this Control's ControlKeywords |
| MatchScore | decimal | similarity score from the fuzzy/word-overlap comparison, used for sorting |
| IsSelfAssessmentDiscrepancy | bool | true when Self-Reported State claimed compliance but this match implies a violation |
| ReviewStatus | enum `ViolationMatchReviewStatus` | Open / Confirmed / Dismissed |
| CreatedAt | DateTime | |

Uniqueness: one `ViolationMatch` per (`IssueId`, `ControlId`) pair — rerunning
matching for the same Issue updates existing rows rather than duplicating.

## Enums (additions to `02-data-model.md`)
```
enum IssueMatchingStatus { Pending, Matched, Failed }
enum ViolationMatchReviewStatus { Open, Confirmed, Dismissed }
```

## Relationships (additions to `02-data-model.md`)
```
Organization 1---* Issue
Control 1---* ControlKeyword
Issue 1---* ViolationMatch *---1 Control
```

## Matching pipeline
Matching is **manually triggered**, not automatic/background. Flow:

1. Admin creates an Issue (Title, Description, RootCause). Saved immediately
   with `MatchingStatus = Pending`. No LLM call happens yet — Issue creation
   is never blocked by matching.
2. Admin clicks **"Run matching"** on the Issue. This is a synchronous
   request that:
   a. Calls `IKeywordExtractionService.ExtractKeywordsAsync(issue.RootCause)`
      — the configured LLM provider extracts a list of keywords/phrases from
      the Root Cause text.
   b. For every `ControlKeyword` across all frameworks, computes a
      normalized fuzzy/word-overlap similarity score (lowercase, trim, basic
      stemming, Levenshtein/partial-ratio-style comparison — hand-rolled, no
      new package) against each extracted keyword.
   c. Any `ControlKeyword` scoring above a configured threshold produces (or
      updates) a `ViolationMatch` for that Control, storing the matched
      keywords and score.
   d. For each resulting `ViolationMatch`, looks up the Control's current
      Self-Reported State (latest relevant `AssessmentResult.Status` and/or
      `SurveyResponse.ResponseLevel` for that Organization+Control). If the
      Self-Reported State indicates compliance (`Compliant` / `High` /
      `VeryHigh`) while a match was found, sets
      `IsSelfAssessmentDiscrepancy = true`.
   e. Sets `Issue.MatchingStatus = Matched` (even if zero matches were
      found — zero matches is a valid, successful outcome) and
      `MatchedAt = now`.
3. If step 2a/2b/2c throws (LLM call fails, times out, or errors), the Issue
   is left as-is except `MatchingStatus = Failed` and `MatchingError` is
   populated with a short diagnostic message. The admin can click "Run
   matching" again at any time to retry — this is not a terminal state.

## LLM provider abstraction
- `IKeywordExtractionService` — single method,
  `Task<IReadOnlyList<string>> ExtractKeywordsAsync(string rootCauseText)`.
- Default implementation: `OpenAICompatibleKeywordExtractionService`, calling a
  configured OpenAI-compatible `chat/completions` endpoint over HTTP or HTTPS.
- `GitHubModelsKeywordExtractionService` remains for reference and tests only;
  GitHub Models was retired on July 30, 2026.
- Provider selection is config-driven: `appsettings.json` →
  `IssueMatching:LlmProvider` (for example, `"OpenAICompatible"`), read at
  startup by a
  factory/DI registration that resolves the concrete implementation. Adding
  a new provider means adding a new implementation class + a case in the
  factory — no changes to calling code (`11-issue-matching` feature code
  only ever depends on the interface).
- Relevant config keys:
  - `IssueMatching:LlmProvider` — provider selector
  - `IssueMatching:OpenAICompatible:Endpoint` — full chat completions URL
  - `IssueMatching:OpenAICompatible:Model` — model or deployment name
  - `IssueMatching:OpenAICompatible:ApiKey` — optional key from environment or
    user secrets, never committed
  - `IssueMatching:OpenAICompatible:ApiKeyHeaderName` and `AuthScheme` —
    authentication overrides for services such as Azure OpenAI
  - `IssueMatching:OpenAICompatible:TimeoutSeconds` — HTTP request timeout
  - `IssueMatching:MatchThreshold` — minimum similarity score to create a
    `ViolationMatch`

## Prioritization / display order
On the Issues/Violation Matches page, matches are sorted:
1. `IsSelfAssessmentDiscrepancy = true` first (highest priority — the org's
   own records disagree with what actually happened).
2. Then by `MatchScore` descending.
3. `Dismissed` matches are shown collapsed/de-prioritized but not deleted.

## Confirm/dismiss workflow
- New dedicated page listing Issues; expanding an Issue shows its
  `ViolationMatch` rows with the Control's Title, Description, GuidanceUrl,
  matched keywords, score, and discrepancy flag.
- Each match has **Confirm** and **Dismiss** buttons — single click, no
  reason field required in this version.
- Confirm/Dismiss only updates `ViolationMatch.ReviewStatus`; it never
  touches `AssessmentResult` or `SurveyResponse`.

## Validation
- `Issue.RootCause` is required (matching has nothing to extract from
  otherwise) but `Description` alone is sufficient to save the Issue —
  `RootCause` can be filled in later before running matching.
- Running matching on an Issue with no `RootCause` is blocked client-side
  with a clear message ("Add a Root Cause before running matching").
- `ControlKeyword.Keyword` must be non-empty; duplicate keywords on the same
  Control are allowed but redundant (no uniqueness constraint enforced).

## Tracing requirements
Following `06-tech-stack.md`'s tracing rules:
- `IssueCreated` — `IssueId`, `OrganizationId`.
- `IssueMatchingStarted` / `IssueMatchingSucceeded` / `IssueMatchingFailed` —
  `IssueId`, elapsed ms, match count, error detail (on failure). Do not log
  raw `RootCause`/`Description` text (sensitive-data rule) — log only
  metadata (length, match count).
- `ViolationMatchReviewed` — `ViolationMatchId`, `IssueId`, `ControlId`, new
  `ReviewStatus`.

## Relationship to other features
- Reuses `07-survey.md`'s `SurveyResponse` and `02-data-model.md`'s
  `AssessmentResult` as the two Self-Reported State sources — no new survey
  concept is introduced.
- Does not modify `05-dashboard.md` in this version; a future dashboard
  widget surfacing open Self-Assessment Discrepancies is a wishlist item.

## Wishlist (explicitly deferred)
- LLM-generated tailored remediation suggestions (beyond static Control
  guidance).
- Automatic/background matching (hosted-service queue) instead of manual
  trigger.
- Dismiss-reason capture, for tuning `ControlKeyword` quality over time.
- Dashboard widget for Self-Assessment Discrepancies.
