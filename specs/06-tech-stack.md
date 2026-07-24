# 06 — Tech Stack & Coding Ground Rules

## Platform
- **Language**: C#
- **Runtime**: **.NET Core 10**
- **Web framework**: ASP.NET Core, using **Blazor Server** (or Razor
  Pages/MVC — final choice made at Phase 1 bootstrap time, documented here
  once decided) to deliver a dynamic, server-rendered website with
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

## Project layout (proposed, refined at bootstrap)
```
BPRadar/
  specs/                     # this folder — source of truth for requirements
  src/
    BPRadar.Web/             # ASP.NET Core (Blazor Server) project
      Data/                  # DbContext, EF Core entities, migrations
      Features/
        Frameworks/          # framework/domain/control seed + read APIs
        Assessments/         # assessment CRUD
        ManualEntry/         # checklist UI (03-manual-entry.md)
        Import/              # CSV/XLSX import pipeline (04-import.md)
        Dashboard/           # dashboard + radar chart (05-dashboard.md)
      wwwroot/
  tests/
    BPRadar.Tests/           # unit tests (scoring logic, import validation, etc.)
  BPRadar.sln
  README.md
  .gitignore                 # excludes bpradar.db, bin/, obj/
```

## Non-functional requirements
- Runs locally with `dotnet run` — no external services required beyond the
  .NET SDK.
- No authentication/authorization in MVP (see `00-overview.md` non-goals).
- No telemetry/analytics collection.

## Out of scope (recap from 00-overview.md)
- Auth/roles, live Microsoft API integration (e.g. Defender for Cloud),
  trend-over-time analytics, PDF export, multi-tenant hosting.
