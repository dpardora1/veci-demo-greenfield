#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Lint specs under docs/specs/ against the VECI playbook rules.

.DESCRIPTION
    Validates each SPEC-YYYY-NNNN-*.md file in the configured root:
      - Frontmatter YAML present and parseable.
      - Mandatory frontmatter fields populated.
      - `id` matches `SPEC-YYYY-NNNN`.
      - `status` is one of the allowed values.
      - `work_item` present when status in {approved, implementing, done}.
      - `superseded_by` present when status == superseded.
      - When `ai_assisted: true`: `ai_model` and `ai_use_id` present.
      - Mandatory sections present: "## 1.", "## 2.", "## 5.", "## 6.", "## 11.".
      - At least one ```gherkin block containing `Feature:` and `Scenario:`.

    Exits 0 on success, 1 on any error. Designed for local dev and CI (GitHub Actions / Azure Pipelines).

.PARAMETER Path
    Root folder to scan. Defaults to ./docs/specs from current directory.

.PARAMETER IgnoreSubfolders
    Subfolder names to skip (default: reviews).

.EXAMPLE
    pwsh tools/lint-spec.ps1
    pwsh tools/lint-spec.ps1 -Path docs/specs
#>
[CmdletBinding()]
param(
    [string]$Path = 'docs/specs',
    [string[]]$IgnoreSubfolders = @('reviews')
)

$ErrorActionPreference = 'Stop'

$AllowedStatuses = @('draft', 'review', 'approved', 'implementing', 'done', 'superseded')
$WorkItemRequiredFor = @('approved', 'implementing', 'done')
$MandatoryFrontmatter = @('id', 'title', 'slug', 'status', 'owner_funcional', 'owner_tecnico', 'created', 'updated')
$MandatorySections = @('## 1.', '## 2.', '## 5.', '## 6.', '## 11.')

function Write-LintError {
    param([string]$File, [int]$Line, [string]$Code, [string]$Message)
    $script:Errors += [pscustomobject]@{ File = $File; Line = $Line; Code = $Code; Message = $Message }
    Write-Host ("{0}:{1}: [{2}] {3}" -f $File, $Line, $Code, $Message) -ForegroundColor Red
}

function Get-Frontmatter {
    param([string[]]$Lines, [string]$File)

    if ($Lines.Count -lt 3 -or $Lines[0].Trim() -ne '---') {
        Write-LintError $File 1 'SPEC-FM-001' 'Spec must start with YAML frontmatter delimiter ---.'
        return $null
    }

    $endIdx = -1
    for ($i = 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].Trim() -eq '---') { $endIdx = $i; break }
    }
    if ($endIdx -lt 0) {
        Write-LintError $File 1 'SPEC-FM-002' 'Closing --- of frontmatter not found.'
        return $null
    }

    $fm = [ordered]@{}
    $fm['__endLine__'] = $endIdx + 1
    for ($i = 1; $i -lt $endIdx; $i++) {
        $raw = $Lines[$i]
        $trim = $raw.Trim()
        if (-not $trim -or $trim.StartsWith('#')) { continue }
        $colon = $trim.IndexOf(':')
        if ($colon -lt 1) { continue }
        $key = $trim.Substring(0, $colon).Trim()
        $val = $trim.Substring($colon + 1).Trim()
        # Strip surrounding quotes
        if (($val.StartsWith('"') -and $val.EndsWith('"')) -or ($val.StartsWith("'") -and $val.EndsWith("'"))) {
            $val = $val.Substring(1, $val.Length - 2)
        }
        $fm[$key] = $val
    }
    return $fm
}

function Test-FieldEmpty {
    param($Value)
    return ($null -eq $Value) -or ($Value -eq '') -or ($Value -eq 'null') -or ($Value -eq '[]') -or ($Value -eq '~')
}

$script:Errors = @()

$root = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
if (-not $root) {
    Write-Host "Spec folder not found: $Path" -ForegroundColor Yellow
    Write-Host "No specs to lint. OK."
    exit 0
}

$files = Get-ChildItem -LiteralPath $root -Filter 'SPEC-*.md' -File -Recurse |
    Where-Object {
        $rel = $_.FullName.Substring($root.Path.Length).TrimStart('\', '/')
        $parts = $rel -split '[\\/]'
        -not ($parts | Where-Object { $IgnoreSubfolders -contains $_ })
    }

if (-not $files) {
    Write-Host "No spec files found under $root. OK."
    exit 0
}

Write-Host ("Linting {0} spec file(s) under {1}..." -f $files.Count, $root) -ForegroundColor Cyan

foreach ($f in $files) {
    $rel = $f.FullName.Substring($root.Path.Length).TrimStart('\', '/')
    $display = (Join-Path (Split-Path $Path -Leaf) $rel) -replace '\\', '/'
    $lines = Get-Content -LiteralPath $f.FullName

    $fm = Get-Frontmatter -Lines $lines -File $display
    if (-not $fm) { continue }

    # Mandatory fields
    foreach ($field in $MandatoryFrontmatter) {
        if (-not $fm.Contains($field) -or (Test-FieldEmpty $fm[$field])) {
            Write-LintError $display 2 'SPEC-FM-010' "Mandatory frontmatter field '$field' is missing or empty."
        }
    }

    # id format
    if ($fm.Contains('id') -and $fm['id']) {
        $expectedId = ($f.BaseName -split '-')[0..2] -join '-'
        if ($fm['id'] -notmatch '^SPEC-\d{4}-\d{4}$') {
            Write-LintError $display 2 'SPEC-FM-011' "Field 'id' must match SPEC-YYYY-NNNN. Got '$($fm['id'])'."
        } elseif ($fm['id'] -ne $expectedId) {
            Write-LintError $display 2 'SPEC-FM-012' "Field 'id' ($($fm['id'])) does not match filename prefix ($expectedId)."
        }
    }

    # status
    $status = $fm['status']
    if ($status -and ($AllowedStatuses -notcontains $status)) {
        Write-LintError $display 2 'SPEC-FM-020' "Field 'status' must be one of: $($AllowedStatuses -join ', '). Got '$status'."
    }

    # work_item required for advanced statuses
    if ($status -and ($WorkItemRequiredFor -contains $status)) {
        if (-not $fm.Contains('work_item') -or (Test-FieldEmpty $fm['work_item'])) {
            Write-LintError $display 2 'SPEC-FM-021' "Status '$status' requires non-empty 'work_item'."
        }
    }

    # superseded_by required when superseded
    if ($status -eq 'superseded') {
        if (-not $fm.Contains('superseded_by') -or (Test-FieldEmpty $fm['superseded_by'])) {
            Write-LintError $display 2 'SPEC-FM-022' "Status 'superseded' requires non-empty 'superseded_by'."
        }
    }

    # AI trazability
    if ($fm['ai_assisted'] -eq 'true') {
        foreach ($aiField in @('ai_model', 'ai_use_id')) {
            if (-not $fm.Contains($aiField) -or (Test-FieldEmpty $fm[$aiField])) {
                Write-LintError $display 2 'SPEC-FM-030' "When ai_assisted=true, field '$aiField' must be set (EU AI Act traceability)."
            }
        }
    }

    # Mandatory sections
    $bodyStart = [int]$fm['__endLine__']
    $bodyLines = $lines[$bodyStart..($lines.Count - 1)]
    foreach ($section in $MandatorySections) {
        $found = $false
        for ($i = 0; $i -lt $bodyLines.Count; $i++) {
            if ($bodyLines[$i] -like "$section*") { $found = $true; break }
        }
        if (-not $found) {
            Write-LintError $display $bodyStart 'SPEC-SEC-001' "Mandatory section '$section' not found."
        }
    }

    # Gherkin block with Feature: and Scenario:
    $inGherkin = $false
    $gherkinBuf = New-Object System.Text.StringBuilder
    $hasFeature = $false
    $hasScenario = $false
    foreach ($line in $bodyLines) {
        if ($line -match '^```gherkin\s*$') { $inGherkin = $true; [void]$gherkinBuf.Clear(); continue }
        if ($inGherkin -and $line -match '^```\s*$') {
            $block = $gherkinBuf.ToString()
            if ($block -match '(?m)^\s*Feature:') { $hasFeature = $true }
            if ($block -match '(?m)^\s*Scenario:') { $hasScenario = $true }
            $inGherkin = $false
            continue
        }
        if ($inGherkin) { [void]$gherkinBuf.AppendLine($line) }
    }
    if (-not $hasFeature -or -not $hasScenario) {
        Write-LintError $display $bodyStart 'SPEC-GHK-001' 'No ```gherkin block with both Feature: and Scenario: found.'
    }
}

Write-Host ''
if ($script:Errors.Count -gt 0) {
    Write-Host ("FAIL: {0} error(s) found across {1} file(s)." -f $script:Errors.Count, $files.Count) -ForegroundColor Red
    exit 1
}

Write-Host ("OK: {0} spec file(s) passed all checks." -f $files.Count) -ForegroundColor Green
exit 0
