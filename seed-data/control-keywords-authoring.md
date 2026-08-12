# Control Keyword authoring prompt

Use this prompt for the offline, one-time LLM-assisted authoring pass described
in `specs/11-issue-matching.md`. It intentionally produces candidate data only;
a human reviews the output before replacing `control-keywords.json`.

## Input preparation

From each file in `frameworks/*.json`, create one input object per control:

```json
{
  "frameworkCode": "AZURE_WAF",
  "controlCode": "RE:09",
  "title": "Disaster recovery plans",
  "description": "Maintain a tested, documented DR plan covering the whole system, not just pieces."
}
```

The following PowerShell emits the complete input array from the current
catalogs without changing source data:

```powershell
$items = foreach ($path in Get-ChildItem .\seed-data\frameworks\*.json) {
    $catalog = Get-Content $path.FullName -Raw | ConvertFrom-Json
    foreach ($domain in $catalog.domains) {
        foreach ($control in $domain.controls) {
            [ordered]@{
                frameworkCode = $catalog.framework.code
                controlCode = $control.code
                title = $control.title
                description = $control.description
            }
        }
    }
}
$items | ConvertTo-Json -Depth 4
```

## Prompt

```text
You are authoring static Control Keywords for BPRadar. BPRadar extracts short
keywords from an operational Issue's known Root Cause, then fuzzy-matches those
phrases against this fixture to identify plausibly implicated best-practice
controls.

For every input control, return exactly one object with the same frameworkCode
and controlCode plus 2-5 candidate keywords.

Authoring rules:
- Write short lowercase phrases likely to occur in a real root-cause statement.
- Prefer concrete failure symptoms, omissions, misconfigurations, and common
  operational synonyms that distinguish this control from neighboring controls.
- Do not merely repeat a broad title when a more diagnostic phrase is possible.
- Never use generic singleton filler such as "security", "risk", "management",
  "process", "policy", "control", "monitoring", "compliance", or "failure".
- Keep true equivalents across frameworks aligned when the same root cause
  should implicate both controls.
- Do not copy or reconstruct paid ISO requirement text. Use only the supplied
  title and original paraphrased description.
- Do not add commentary and do not omit any input control.

Return JSON in this shape:
[
  {
    "frameworkCode": "AZURE_WAF",
    "controlCode": "RE:09",
    "keywords": [
      "disaster recovery plan",
      "untested recovery procedure",
      "regional recovery failure"
    ]
  }
]

INPUT CONTROLS:
<paste the generated input array here>
```

## Human review

Group the candidates by framework/domain and compare adjacent controls. Replace
neutral title paraphrases with likely root-cause language, remove accidental
cross-control collisions, preserve intentional overlap for equivalent controls,
and merge reviewed candidates into `control-keywords.json`. Then update
`authoring.lastReviewed` and run
`seed-data/tests/control-keywords.tests.ps1`.
