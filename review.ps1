# Set REVIEWER_TOOL to "codex" or "claude" to skip the selection prompt.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Fail {
    param([string]$Message)

    [Console]::Error.WriteLine("Error: $Message")
    exit 1
}

if ($args.Count -ne 0) {
    Fail "This script does not accept arguments."
}

$codex = Get-Command codex -ErrorAction SilentlyContinue
$claude = Get-Command claude -ErrorAction SilentlyContinue
$tool = $env:REVIEWER_TOOL
$showTip = $false

if (-not [string]::IsNullOrEmpty($tool)) {
    switch -CaseSensitive ($tool) {
        "codex" {
            if ($null -eq $codex) { Fail "REVIEWER_TOOL selects 'codex', but Codex CLI was not found in PATH." }
        }
        "claude" {
            if ($null -eq $claude) { Fail "REVIEWER_TOOL selects 'claude', but Claude Code was not found in PATH." }
        }
        default { Fail "REVIEWER_TOOL must be 'codex' or 'claude'." }
    }

    Write-Host "Using $tool from REVIEWER_TOOL."
}
elseif ($null -ne $codex -and $null -ne $claude) {
    Write-Host "Choose a reviewer:"
    Write-Host "  1) Codex"
    Write-Host "  2) Claude Code"
    $selection = try { Read-Host "Selection [1-2]" } catch { $null }

    if ($null -eq $selection) {
        Fail "Could not read reviewer selection."
    }

    switch ($selection.Trim()) {
        "1" { $tool = "codex" }
        "2" { $tool = "claude" }
        default { Fail "Invalid selection." }
    }

    $showTip = $true
}
elseif ($null -ne $codex) {
    $tool = "codex"
}
elseif ($null -ne $claude) {
    $tool = "claude"
}
else {
    Fail "Install Codex CLI or Claude Code before running a review."
}

$global:LASTEXITCODE = 0
if ($tool -eq "codex") {
    Write-Host "Running Codex review..."
    & codex /review
    $commandSucceeded = $?
}
else {
    Write-Host "Running Claude Code review..."
    & claude /code-review
    $commandSucceeded = $?
}

$exitCode = $LASTEXITCODE
if (-not $commandSucceeded -and $exitCode -eq 0) {
    $exitCode = 1
}

if ($showTip) {
    Write-Host "Tip: add `$env:REVIEWER_TOOL = `"$tool`" to your PowerShell profile to skip this prompt."
}

exit $exitCode
