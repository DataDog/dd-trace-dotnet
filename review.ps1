Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Fail {
    param([string]$Message)

    Write-Error $Message
    exit 1
}

if ($args.Count -ne 0) {
    Fail "This script does not accept arguments."
}

$hasCodex = $null -ne (Get-Command codex -ErrorAction SilentlyContinue)
$hasClaude = $null -ne (Get-Command claude -ErrorAction SilentlyContinue)

if ($hasCodex -and $hasClaude) {
    Write-Host "Choose a reviewer:"
    Write-Host "  1) Codex"
    Write-Host "  2) Claude Code"
    $selection = Read-Host "Selection [1-2]"

    switch ($selection) {
        "1" { $tool = "codex" }
        "2" { $tool = "claude" }
        default { Fail "Invalid selection." }
    }
}
elseif ($hasCodex) {
    $tool = "codex"
}
elseif ($hasClaude) {
    $tool = "claude"
}
else {
    Fail "Install Codex CLI or Claude Code before running a review."
}

if ($tool -eq "codex") {
    Write-Host "Running Codex review..."
    & codex review
    exit $LASTEXITCODE
}

Write-Host "Running Claude Code review..."
& claude --permission-mode plan -p "/code-review"
exit $LASTEXITCODE
