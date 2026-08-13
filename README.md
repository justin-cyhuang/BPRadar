# BPRadar

BPRadar is a .NET 10 Razor Pages application for assessing organizations against
ISO/IEC 27001, ISO/IEC 20000-1, and the Azure Well-Architected Framework. An
optional LLM-assisted pipeline extracts failure keywords from an Issue's Root
Cause and matches them deterministically to curated Control Keywords.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

## Run locally

```powershell
git clone https://github.com/justin-cyhuang/BPRadar.git
Set-Location BPRadar
dotnet restore
dotnet build
Set-Location src\BPRadar.Web
dotnet run
```

Open the HTTPS or HTTP URL printed by `dotnet run`. The root URL redirects to
the survey-template administration page.

At startup, `Program.cs` applies all EF Core migrations and runs the idempotent
`DatabaseSeeder`. With the default connection string, SQLite stores its database
at `src\BPRadar.Web\bpradar.db` when launched from that directory. Override
`ConnectionStrings:Default` to use another location.

## Seed data

Startup loads three frameworks, three survey templates with 49 questions, and
184 controls. Issue matching uses a static catalog with one Control Keyword
entry per control: 184 entries and 552 curated phrases.

See [`seed-data/README.md`](seed-data/README.md) for fixture contents, catalog
scope, validation, and the Control Keyword refresh process.

## Configure issue matching

Configuration lives under `IssueMatching`:

```json
{
  "IssueMatching": {
    "LlmProvider": "OpenAICompatible",
    "MatchThreshold": 0.72,
    "ControlKeywordSeedPath": "seed-data/control-keywords.json",
    "OpenAICompatible": {
      "Endpoint": "https://api.openai.com/v1/chat/completions",
      "Model": "gpt-4.1-mini",
      "ApiKey": null,
      "TimeoutSeconds": 30,
      "ApiKeyHeaderName": "Authorization",
      "AuthScheme": "Bearer"
    }
  }
}
```

- `OpenAICompatible` is the production provider. It targets OpenAI, Azure
  OpenAI, or self-hosted runtimes such as Ollama, vLLM, and LM Studio.
- `GitHubModels` is retained only for reference and tests. GitHub Models was
  retired on July 30, 2026 and should not be selected for production.

Never commit an API key to `appsettings.json`. From `src\BPRadar.Web`, store it
in .NET user secrets:

```powershell
dotnet user-secrets set "IssueMatching:OpenAICompatible:ApiKey" "your-api-key"
```

The equivalent environment variable is
`IssueMatching__OpenAICompatible__ApiKey`:

```powershell
$env:IssueMatching__OpenAICompatible__ApiKey = "your-api-key"
dotnet run
```

### OpenAI

Use the defaults shown above, changing `Model` if needed. Authentication uses
the `Authorization` header with the `Bearer` scheme by default.

### Azure OpenAI

Azure OpenAI API-key authentication requires the raw key in the `api-key`
header, rather than an authorization bearer header:

```json
{
  "IssueMatching": {
    "LlmProvider": "OpenAICompatible",
    "OpenAICompatible": {
      "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/openai/deployments/YOUR-DEPLOYMENT/chat/completions?api-version=2024-10-21",
      "Model": "YOUR-DEPLOYMENT",
      "ApiKey": null,
      "TimeoutSeconds": 30,
      "ApiKeyHeaderName": "api-key",
      "AuthScheme": null
    }
  }
}
```

Supply the Azure key with the same user-secrets command. Microsoft Entra bearer
tokens can also be supplied with the `Authorization`/`Bearer` defaults, but
BPRadar does not acquire or refresh those tokens.

### Ollama or another local runtime

Start an OpenAI-compatible local server and point BPRadar at it. For Ollama:

```json
{
  "IssueMatching": {
    "LlmProvider": "OpenAICompatible",
    "OpenAICompatible": {
      "Endpoint": "http://localhost:11434/v1/chat/completions",
      "Model": "llama3.2",
      "ApiKey": null,
      "TimeoutSeconds": 60,
      "ApiKeyHeaderName": "Authorization",
      "AuthScheme": "Bearer"
    }
  }
}
```

When `ApiKey` is null, empty, or whitespace, BPRadar sends no authentication
header.

## Main pages

| Page | Purpose |
|---|---|
| `/Admin/SurveyTemplates` | Create and manage recurring survey templates |
| `/Admin/Issues` | Review organization Issues and matching status |
| `/Organizations/Surveys` | Select an organization and complete surveys |
| `/Organizations/Issues` | Select an organization and manage its Issues |

## Tests

From the repository root:

```powershell
dotnet test
```

## Architecture

The application is a single ASP.NET Core Razor Pages project backed by EF Core
and SQLite. Feature code lives under `src\BPRadar.Web\Features`; migrations and
the database context live under `src\BPRadar.Web\Data`. Issue matching isolates
the LLM call behind `IKeywordExtractionService`, then performs Control Keyword
matching in application code so results stay inspectable and testable.

See [`specs/`](specs/) for the feature specifications and
[`docs/adr/0001-llm-based-issue-matching.md`](docs/adr/0001-llm-based-issue-matching.md)
for the LLM boundary decision.
