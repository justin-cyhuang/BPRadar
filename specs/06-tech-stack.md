# 06 — Tech Stack & Coding Ground Rules

## Platform
- **Language**: C#
- **Runtime**: **.NET Core 10**
- **Web framework**: ASP.NET Core using **Razor Pages** to deliver a dynamic,
  server-rendered website with
  interactive components (checklist forms, import wizard, dashboard/radar
  chart) without needing a separate JS framework/build pipeline.
- **Single deployable**: one ASP.NET Core project hosts both the UI and the
  data access layer — no separate frontend/backend split.

## Data access
- **ORM**: Entity Framework Core (`Microsoft.EntityFrameworkCore`)
- **Database provider**: SQLite via `Microsoft.EntityFrameworkCore.Sqlite`
  (which wraps `Microsoft.Data.Sqlite`)
- Database file (`bpradar.db`) is local, git-ignored; EF Core Migrations are
  committed to source control so the schema is reproducible.

## File parsing
- **CSV**: built-in .NET APIs (`System.IO`, `Microsoft.VisualBasic.FileIO.
  TextFieldParser` — Microsoft-published, ships with .NET)
- **XLSX**: `DocumentFormat.OpenXml` (Microsoft's Open XML SDK)

## Charting
- No third-party charting package. The radar/spider chart in
  `05-dashboard.md` is implemented as a custom Razor/SVG component.

## Reporting exports
- **CSV export** is generated server-side using built-in .NET I/O/encoding.
- **PDF export** is delivered via a print-friendly report view (HTML/CSS)
  designed for browser Save-as-PDF audit handoff, avoiding non-Microsoft
  dependencies while still producing a consistent report layout.

## ★ Ground rule: Microsoft-published packages only
Every package reference (NuGet, and any client-side script/library if ever
needed) used in this project **must be published by Microsoft**. Examples of
allowed packages:
- `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`,
  `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.Data.Sqlite`
- `DocumentFormat.OpenXml`
- ASP.NET Core / .NET runtime built-ins (no separate package needed)

If a need arises for which **no Microsoft-published package exists** (e.g.
radar charting, advanced CSV edge cases), the functionality is implemented
in-house rather than pulling in a third-party dependency. Any exception to
this rule requires explicit user approval before being added, and must be
recorded here with justification.

### Recorded exception: LLM service for issue-matching keyword extraction
`11-issue-matching.md` calls an **OpenAI-compatible chat completions API** to
extract keywords from Issue root causes. This external or locally hosted service
is a deliberate, explicitly-approved exception scoped to that one feature's
keyword-extraction step only — see
`docs/adr/0001-llm-based-issue-matching.md` for rationale. It is called behind an
`IKeywordExtractionService` interface with config-driven provider selection.
The retired GitHub Models implementation remains for reference and tests only.
Everything else in the app remains local-only and Microsoft-package-only,
including the keyword-to-`ControlKeyword` fuzzy matching step in that same
feature, which is hand-rolled with no new package.

## API-key authentication for `/api/*`
The MVP ships with no login system, but the JSON `/api/*` surface (all
`Map{Get,Post,Put,Delete}` minimal-API endpoints, including
`11-issue-matching.md`'s Issue-creation endpoint) supports an optional
API-key check so it can later be trusted to accept pushes from an external
system (e.g. an incident/ticketing tool posting an Issue), without adding a
full login/roles system.

- **Off by default.** For local/demo use, no key is required.
- Config keys:
  - `Api:RequireApiKey` (bool, default `false`) — when `true`, every `/api/*`
    request must include a valid key.
  - `Api:ApiKey` (string) — the expected key value; sourced from user
    secrets/environment, never committed to source control.
- Request header: `X-Api-Key`. A request to `/api/*` missing the header or
  presenting the wrong value when `RequireApiKey=true` is rejected with
  `401 Unauthorized`.
- Implemented as a custom ASP.NET Core `AuthenticationHandler` (no external
  identity provider, no `Microsoft.AspNetCore.Identity`) — consistent with
  the Microsoft-published-packages-only rule and the no-login-system MVP
  scope; it authenticates the caller, it does not add per-user roles.
- **Fail fast at startup**: if `Api:RequireApiKey=true` but `Api:ApiKey` is
  empty/unset, the app must refuse to start rather than silently accepting
  all requests or silently rejecting all requests at runtime.
- Razor Pages (the UI) are unaffected — this gate applies only to `/api/*`.

## Tracing and diagnostics (required)
- Built-in tracing is mandatory for debuggability, using
  `System.Diagnostics.TraceSource` (source name: `BPRadar`) as the project
  trace backbone.
- Trace level is environment-configurable (development/staging/production)
  so the same code can run with different verbosity without recompilation.
- Correlation is required on every request and long-running operation:
  - Accept incoming `X-Correlation-ID` when present; otherwise generate one.
  - Set `Trace.CorrelationManager.ActivityId` per request/operation.
  - Include the correlation ID in all trace lines and error responses.
- Minimum required trace points:
  - Request start/end for write operations (create/update/import).
  - Manual assessment upsert attempts and outcomes.
  - Import preview and import commit summary (rows read/valid/invalid/upserted).
  - Survey submission lifecycle (start/save/finalize) and trend computation.
  - Dashboard metric computation start/end and elapsed time.
  - Unhandled exception boundary with correlation ID and operation context.
- Minimum required trace fields per event:
  - UTC timestamp (ISO 8601), severity, component, operation name,
    correlation ID, duration ms (when applicable),
    and key business IDs when available (`OrganizationId`, `AssessmentId`,
    `FrameworkId`, `ControlId`).
- Sensitive-data rule for tracing:
  - Do not log raw uploaded file contents, free-form Notes, or Evidence URLs.
  - Log only metadata (file name, byte size, row count, hash) and validation
    summaries needed for debugging.
- Trace listeners:
  - Development: `ConsoleTraceListener`.
  - Local runtime: `TextWriterTraceListener` to `logs/` with daily file
    naming; retention cleanup (e.g. keep last 14 days) handled by app startup
    housekeeping code.

## Project layout (proposed, refined at bootstrap)
```
BPRadar/
  specs/                     # this folder — source of truth for requirements
  src/
    BPRadar.Web/             # ASP.NET Core Razor Pages project
      Data/                  # DbContext, EF Core entities, migrations
      Features/
        Frameworks/          # framework/domain/control seed + read APIs
        Assessments/         # assessment CRUD
        Baselines/           # baseline/target profile management
        Surveys/             # recurring company profile survey and submissions
        ManualEntry/         # checklist UI (03-manual-entry.md)
        Import/              # CSV/XLSX import pipeline (04-import.md)
        Dashboard/           # dashboard + radar chart (05-dashboard.md)
        Reporting/           # CSV/PDF audit handoff exports
        IssueMatching/       # issue/root cause capture + LLM keyword
                             # extraction + control matching (11-issue-matching.md)
      wwwroot/
  tests/
    BPRadar.Tests/           # unit tests (scoring logic, import validation, etc.)
  BPRadar.sln
  README.md
  .gitignore                 # excludes bpradar.db, bin/, obj/
```

## Non-functional requirements
- Runs locally with `dotnet run` — no external services required beyond the
  .NET SDK, **except** `11-issue-matching.md`'s keyword-extraction call to
  a configured OpenAI-compatible endpoint (see recorded exception above); the
  rest of the app has no external service dependency.
- No authentication/authorization for the UI in MVP (see `00-overview.md`
  non-goals). The `/api/*` surface has an optional API-key gate — see
  "API-key authentication for `/api/*`" below.
- No telemetry/analytics collection.

## Out of scope (recap from 00-overview.md)
- Auth/roles, live Microsoft API integration (e.g. Defender for Cloud),
  predictive analytics/forecasting, multi-tenant hosting.
