# Optional API-key authentication for the `/api/*` surface

BPRadar's MVP ships with no login system at all (`00-overview.md` non-goals).
`11-issue-matching.md` assumes Issues can originate from an external process
(`CONTEXT.md`: "a problem opened in an external system"), and an existing
endpoint (`POST /api/organizations/{organizationId}/issues`) already accepts
that data as plain JSON with no caller identity check. For the current
hackathon demo we want that endpoint reachable with zero friction, but we do
not want "add auth" to require reworking the endpoint or its callers later —
so the auth mechanism is built now, disabled by default, and turned on with
one config flag.

## Considered options
- **Do nothing until a real external caller shows up** — rejected: retrofitting
  auth onto an endpoint after external systems already depend on its request
  shape is more disruptive than building the (off) mechanism up front.
- **ASP.NET Core Identity / cookie login** — rejected: adds a login system to
  the whole app (contradicts the no-login MVP scope) to solve a
  machine-to-machine trust problem, not a human login problem.
- **JWT bearer tokens** — rejected for now: needs something to issue/validate
  tokens (an STS or hand-rolled issuer), which is more machinery than a
  single-tenant hackathon demo with one trusted external caller needs.
- **Custom API-key `AuthenticationHandler`, config-gated, applied to all of
  `/api/*`** (chosen) — smallest mechanism that lets a future external system
  authenticate, keeps the UI (Razor Pages) completely unaffected, and needs no
  external identity provider (consistent with the Microsoft-published-packages
  and no-external-services rules in `06-tech-stack.md`).

## Decision
- `Api:RequireApiKey` (bool, default `false`) and `Api:ApiKey` (secret string)
  config keys; `X-Api-Key` request header.
- Off by default: the hackathon demo runs with no key required.
- Scoped to the whole `/api/*` surface (not just Issues), so there is one
  consistent policy rather than a one-off rule for a single endpoint.
- Fail fast at startup if `RequireApiKey=true` with an empty `ApiKey`, so a
  misconfiguration can never silently degrade into "accept everything" or
  silently degrade into "reject everything" discovered only at request time.

## Consequences
- Today's behavior (no key, anyone who can reach `/api/*` can call it) is
  unchanged until an operator explicitly opts in via config.
- When enabled, all `/api/*` callers — including BPRadar's own Razor Pages, if
  they ever call these endpoints instead of using `DbContext` directly — must
  present the same static key. There is no per-caller key/identity in this
  version; that is a future enhancement if multiple external systems need to
  be distinguished or individually revoked.
- `11-issue-matching.md`'s "no external ticketing integration" scope boundary
  is clarified, not changed: BPRadar still never *pulls* from a ticketing
  system, but an authenticated external system *pushing* an Issue through the
  existing endpoint is an intended, supported path.
