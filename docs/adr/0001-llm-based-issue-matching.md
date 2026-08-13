# LLM-based keyword extraction for issue-to-control matching

BPRadar's ground rules (`06-tech-stack.md`, `00-overview.md`) state
Microsoft-published packages only, no external services, and no ML/advanced
analytics. The issue-matching feature (`11-issue-matching.md`) needs to
turn free-text Root Cause descriptions into candidate Control matches, which
requires natural-language understanding no hand-written rule engine can
provide reliably. We are deliberately overriding those rules for this one
feature: a live LLM call extracts keywords from an Issue's Root Cause text at
matching time. The default implementation calls a configurable
**OpenAI-compatible chat completions endpoint**.

This is an explicit, informed exception (per user decision, hackathon scope),
not an oversight — the rest of the app remains local-only and
Microsoft-package-only.

## Considered options
- **No LLM, keyword-rule engine only** — rejected: cannot generalize to
  free-text Root Cause phrasing without a large hand-authored rule set.
- **LLM does one-stage direct control matching** — rejected: less
  inspectable/tunable than a two-stage extract-then-match pipeline, and
  couples matching quality entirely to the LLM's judgment of all ~184
  controls at once.
- **LLM extracts keywords only; separate deterministic fuzzy-match step
  matches keywords to curated `ControlKeyword` tags** (chosen) — keeps the
  LLM's job narrow (keyword extraction, which it's reliably good at) and
  keeps the matching logic itself deterministic, in-house, and debuggable.

## Provider abstraction
Because "call an LLM" is itself a deviation from the local-only rule, the
call is isolated behind an `IKeywordExtractionService` interface. The
`OpenAICompatibleKeywordExtractionService` supports OpenAI, Azure OpenAI, and
self-hosted runtimes through endpoint, model, and authentication settings.
`GitHubModelsKeywordExtractionService` remains for reference and tests only
because GitHub Models was retired on July 30, 2026. Implementations are selected
via `appsettings.json` (`LlmProvider`) at startup, so callers do not depend on a
specific vendor. See `11-issue-matching.md` for interface/config detail.

## Consequences
- The app has one optional runtime LLM dependency; it can be an external service
  or a local OpenAI-compatible runtime. Everything else remains offline/local.
- Keyword-to-control matching (fuzzy/stemmed comparison) stays hand-rolled
  and Microsoft-package-only — the exception is scoped narrowly to the LLM
  call itself, not the whole feature.
- If the configured LLM endpoint is unavailable, Issue submission still succeeds
  (`11-issue-matching.md` §Matching lifecycle) — matching is best-effort and
  retriable, not a hard dependency for using the rest of the app.
