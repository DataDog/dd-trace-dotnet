# CI Troubleshooting Scripts Reference

## Overview

This document describes reusable PowerShell scripts for Azure DevOps CI troubleshooting, located in `tracer/tools/`.

## Get-AzureDevOpsBuildAnalysis.ps1

**Location:** `tracer/tools/Get-AzureDevOpsBuildAnalysis.ps1`

**Purpose:** Fetches and analyzes Azure DevOps build failures, including timeline data, error messages, failed test extraction, and optional log downloads.

### Prerequisites

- **PowerShell 5.1+** - Required to run this script
  - **Recommended**: PowerShell 7+ (`pwsh`) for cross-platform support
  - **Minimum**: PowerShell 5.1 (`powershell.exe` on Windows)
  - Installation: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell
  - Verify: `pwsh -Version` or `powershell -NoProfile -Command '$PSVersionTable.PSVersion'`
- **Azure CLI** (`az`) authenticated to DataDog organization
- **GitHub CLI** (`gh`) authenticated (only if using `-PullRequest` parameter)

**Note**: This script uses PowerShell-specific features (e.g., `-notin` operator, `HashSet<T>`, `Invoke-RestMethod`) that cannot be easily replicated in bash. Always prefer `pwsh` over `powershell.exe` when both are available.

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `BuildId` | int | Yes (set 1) | - | Azure DevOps build ID to analyze |
| `PullRequest` | int | Yes (set 2) | - | GitHub PR number (resolves build ID via `gh pr checks`) |
| `IncludeLogs` | switch | No | - | Download task logs for failed tasks |
| `OutputPath` | string | No | `$env:TEMP` | Directory for JSON artifacts and logs |

**Note:** `BuildId` and `PullRequest` are mutually exclusive (different parameter sets).

### Usage Examples

#### Basic Analysis by Build ID

```powershell
.\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345
```

Outputs human-readable table summary to console.

#### Analysis by PR Number

```powershell
.\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -PullRequest 8172
```

Resolves build ID from PR checks, then analyzes.

#### Download Logs

```powershell
.\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345 -IncludeLogs -OutputPath D:\temp\ci-logs
```

Downloads task logs for all failed tasks to specified directory.

#### Programmatic Use

```powershell
$analysis = .\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345
$analysis.FailedTests | ForEach-Object { Write-Host $_ }
```

Returns a `PSCustomObject` to the pipeline. Pipe to `ConvertTo-Json -Depth 10` for JSON output.

#### Full Pipeline Example

```powershell
.\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 `
    -PullRequest 8172 `
    -IncludeLogs `
    -OutputPath D:\temp\pr-8172-analysis `
    -Verbose
```

### Output Object Shape

When capturing the returned object, the following structure is provided:

| Field | Type | Description |
|-------|------|-------------|
| `BuildId` | int | Azure DevOps build ID |
| `BuildNumber` | string | Build number (e.g., "20250209.1") |
| `Status` | string | Build status ("completed", "inProgress", etc.) |
| `Result` | string | Build result ("succeeded", "failed", "canceled") |
| `Branch` | string | Source branch (e.g., "refs/heads/master") |
| `Commit` | string | Git commit SHA |
| `FinishTime` | string | ISO 8601 timestamp |
| `FailedTaskCount` | int | Count of failed tasks |
| `FailedTasks` | string[] | Array of failed task names |
| `FailedJobs` | string[] | Array of failed job names |
| `FailedStages` | string[] | Array of failed stage names |
| `CanceledJobs` | string[] | Canceled job names |
| `TimedOutJobs` | string[] | Jobs canceled after ~60 min (format: "name (XX.X min)") |
| `CollateralCanceled` | string[] | Jobs canceled in < 5 min |
| `FailedTests` | string[] | Extracted test names (via regex patterns) |
| `BuildErrors` | string[] | Compilation errors and Nuke target exceptions |
| `ErrorMessages` | string[] | Raw error messages from failed tasks |
| `LogFiles` | string[] | Paths to downloaded log files |
| `ArtifactPath` | string | Directory containing saved JSON files |
| `BuildUrl` | string | Azure DevOps web URL for build |

### Saved Artifacts

The script saves the following files to `OutputPath`:

- `build-{BuildId}-details.json` — Build details from Azure DevOps API
- `build-{BuildId}-timeline.json` — Build timeline records (tasks/jobs/stages)
- `build-{BuildId}-task-{TaskId}-{TaskName}.log` — Task logs (if `-IncludeLogs` used)

### Internal Implementation Details

#### Test Name Extraction (`Extract-FailedTests`)

Failed test names are extracted via regex patterns:

- `\[xUnit\.net...\] ... [FAIL]` — xUnit test names (dot-separated and comma-separated variants)
- `\[FAIL\]\s+([^\r\n]+)` — Generic `[FAIL]` marker
- `Failed\s+([^\r\n]+)` — Generic `Failed` prefix
- `Expected N spans...in/at TestName` — Span count mismatch
- `Received file does not match...TestName.verified.txt` — Snapshot verification
- `The active test run was aborted. Reason: ...` — Test host crashes (CLR fatal errors, access violations)
- `Error testing <framework>` — Framework-specific failures (e.g., `net10.0`, `netcoreapp3.1`)

Crash blocks (lines following "test running when the crash occurred") are also parsed for bare fully-qualified test names.

Duplicates are removed using `HashSet<string>`.

#### Build Error Extraction (`Extract-BuildErrors`)

Compilation errors and build infrastructure failures are extracted separately:

- `error <CODE>: <description>` — Compilation errors with any prefix (CS, NU, NETSDK, SYSLIB, SA, RS, IL, etc.). Paths are normalized to `filename(line,col)`.
- `Target "<name>" has thrown an exception` — Nuke build target failures.

Duplicates are removed using `HashSet<string>`.

#### Log Download Strategy

Logs are downloaded directly from `timeline.records[].log.url` (not via the broken `az devops invoke --resource logs` API which returns HTTP 500).

Uses `Invoke-RestMethod -OutFile` for native PowerShell downloads.

Non-fatal: Individual download failures emit warnings but don't stop execution.

#### Canceled Job Detection

Jobs with `result == "canceled"` are classified by duration:

- **Timeout** (>= 55 min): Job likely hit Azure DevOps 60-minute timeout. Threshold is 55 minutes (not 60) to account for Azure DevOps timing variance and early cancellation.
- **Collateral** (< 5 min): Job canceled quickly, likely due to parent stage failure triggering cascade cancellation.
- **Unknown** (5-55 min): Could be manual cancellation or other causes.

Duration is calculated from `startTime` and `finishTime` fields in the timeline record.

### Exit Codes

- **0:** Success (build analyzed, data returned)
- **1:** Error (missing prerequisites, API failure, invalid PR)

### Verbose Output

Use `-Verbose` to see:

- Azure CLI command invocations
- API call details
- File save locations
- Log download progress

### Known Limitations

1. **Test Name Extraction:** Regex-based; may miss tests with unusual formatting
2. **Log URL Availability:** Some tasks may not have `.log.url` populated
3. **Performance:** Downloads full timeline JSON (can be large for multi-stage pipelines)
4. **Canceled Job Classification:** Duration-based heuristic may misclassify manual cancellations that occur between 5-55 minutes. In practice, most manual cancellations happen quickly (< 5 min) or timeouts occur at ~60 min, so this range is rare.

### Related Documentation

- [Troubleshooting CI Failures Skill](./SKILL.md) — AI agent skill using this script
- [CLAUDE.md](../../../AGENTS.md) — Windows CLI pitfalls and PowerShell conventions
