$ErrorActionPreference = "Stop"

$seedRoot = Split-Path -Parent $PSScriptRoot
$keywordPath = Join-Path $seedRoot "control-keywords.json"
$schemaPath = Join-Path $seedRoot "control-keywords.schema.json"
$catalogPaths = @(
    (Join-Path $seedRoot "frameworks\azure-waf.json"),
    (Join-Path $seedRoot "frameworks\iso27001.json"),
    (Join-Path $seedRoot "frameworks\iso20000.json")
)

if (-not (Test-Path $keywordPath)) {
    throw "Missing Control Keyword fixture: $keywordPath"
}

if (-not (Test-Path $schemaPath)) {
    throw "Missing Control Keyword schema: $schemaPath"
}

$keywordJson = Get-Content $keywordPath -Raw
if (-not ($keywordJson | Test-Json -SchemaFile $schemaPath)) {
    throw "Control Keyword fixture does not match its JSON schema."
}

$fixture = $keywordJson | ConvertFrom-Json
$catalogControls = foreach ($catalogPath in $catalogPaths) {
    $catalog = Get-Content $catalogPath -Raw | ConvertFrom-Json
    foreach ($domain in $catalog.domains) {
        foreach ($control in $domain.controls) {
            "$($catalog.framework.code)|$($control.code)"
        }
    }
}

$keywordControls = foreach ($framework in $fixture.frameworks) {
    foreach ($control in $framework.controls) {
        "$($framework.frameworkCode)|$($control.controlCode)"
    }
}

$missing = @($catalogControls | Where-Object { $_ -notin $keywordControls })
$extra = @($keywordControls | Where-Object { $_ -notin $catalogControls })
$duplicates = @($keywordControls | Group-Object | Where-Object Count -ne 1)

if ($missing.Count -gt 0) {
    throw "Missing controls: $($missing -join ', ')"
}

if ($extra.Count -gt 0) {
    throw "Unknown controls: $($extra -join ', ')"
}

if ($duplicates.Count -gt 0) {
    throw "Duplicate control entries: $($duplicates.Name -join ', ')"
}

if ($keywordControls.Count -ne 184) {
    throw "Expected 184 Control Keyword entries; found $($keywordControls.Count)."
}

Write-Output "Control Keyword seed data is valid: 184 controls, complete catalog coverage, 2-5 normalized keywords each."
