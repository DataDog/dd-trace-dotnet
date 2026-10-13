Set-StrictMode -Version Latest

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

if (-not [string]::IsNullOrEmpty($tool)) {
    switch -CaseSensitive ($tool) {
        "codex" {
            if ($null -eq $codex) { Fail "Codex CLI was not found in PATH." }
        }
        "claude" {
            if ($null -eq $claude) { Fail "Claude Code was not found in PATH." }
        }
        default { Fail "REVIEWER_TOOL must be 'codex' or 'claude'." }
    }
}
elseif ($null -ne $codex -and $null -ne $claude) {
    Write-Host "Choose a reviewer:"
    Write-Host "  1) Codex"
    Write-Host "  2) Claude Code"
    $selection = (Read-Host "Selection [1-2]").Trim()

    switch ($selection) {
        "1" { $tool = "codex" }
        "2" { $tool = "claude" }
        default { Fail "Invalid selection." }
    }
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

if ($tool -eq "codex") {
    Write-Host "Running Codex review..."
    & codex /review
    exit $LASTEXITCODE
}

Write-Host "Running Claude Code review..."
& claude /code-review
exit $LASTEXITCODE
