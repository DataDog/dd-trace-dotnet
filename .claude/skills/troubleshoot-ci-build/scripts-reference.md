# CI Troubleshooting Scripts Reference

## Overview

This document describes reusable PowerShell scripts for Azure DevOps CI troubleshooting, located in `tracer/tools/`.

Both scripts share common functions via the `AzureDevOpsHelpers.psm1` module (auto-imported).

## AzureDevOpsHelpers.psm1

**Location:** `tracer/tools/AzureDevOpsHelpers.psm1`

**Purpose:** Shared module providing common Azure DevOps API functions used by both analysis and retry scripts.

### Exported Functions

| Function | Description |
|----------|-------------|
| `Invoke-AzDevOpsApi` | Calls Azure DevOps REST API via `az devops invoke`. Supports GET/PATCH/POST/PUT, JSON request bodies, and configurable API versions. |
| `Get-BuildIdFromPR` | Resolves an Azure DevOps build ID from a GitHub PR number using `gh pr checks`. |
| `Resolve-BuildId` | Unified build ID resolution: accepts `ByBuildId` (passthrough), `ByPullRequest`, or `ByCurrentBranch` (auto-detect via `gh pr view`). Checks CLI prerequisites. |

### `Invoke-AzDevOpsApi` Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Area` | string | - | API area (e.g., `'build'`) |
| `Resource` | string | - | API resource (e.g., `'builds'`, `'timeline'`, `'stages'`) |
| `RouteParameters` | string | `''` | Space-separated route params (e.g., `'project=dd-trace-dotnet buildId=12345'`) |
| `QueryParameters` | hashtable | `@{}` | Query string parameters |
| `HttpMethod` | string | `'GET'` | HTTP method (`'GET'`, `'PATCH'`, `'POST'`, `'PUT'`) |
| `Body` | hashtable | `$null` | Request body (serialized to JSON, passed via temp file) |
| `ApiVersion` | string | `'6.0'` | Azure DevOps API version |
| `SaveToFile` | string | `''` | Optional path to save JSON response |

### Usage

```powershell
Import-Module .\tracer\tools\AzureDevOpsHelpers.psm1

# GET request
$timeline = Invoke-AzDevOpsApi -Area 'build' -Resource 'timeline' `
    -RouteParameters 'project=dd-trace-dotnet buildId=12345'

# PATCH request with body
Invoke-AzDevOpsApi -Area 'build' -Resource 'stages' `
    -RouteParameters 'project=dd-trace-dotnet buildId=12345 stageRefName=my_stage' `
    -HttpMethod 'PATCH' -Body @{ state = 'retry'; forceRetryAllJobs = $false } `
    -ApiVersion '7.1'

# Resolve build ID from any source
$buildId = Resolve-BuildId -ParameterSetName 'ByPullRequest' -PullRequest 8172
```

## Get-AzureDevOpsBuildAnalysis.ps1

**Location:** `tracer/tools/Get-AzureDevOpsBuildAnalysis.ps1`

**Purpose:** Fetches and analyzes Azure DevOps build failures, including timeline data, error messages, failed test extraction, and optional log downloads.

### Prerequisites

- **PowerShell 7+** (`pwsh`) — recommended; minimum PowerShell 5.1 (`powershell.exe`, Windows only)
  - Verify: `pwsh -Version`
  - Install: `winget install Microsoft.PowerShell` (Windows) · `brew install powershell/tap/powershell` (macOS)
  - Docs: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell
- **Azure CLI** (`az`) with `azure-devops` extension — used for build/timeline API queries
  - Verify: `az version` (check that `azure-devops` appears under "extensions")
  - Install: `winget install Microsoft.AzureCLI` (Windows) · `brew install azure-cli` (macOS), then `az extension add --name azure-devops`
  - Docs: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
- **GitHub CLI** (`gh`) — authenticated; only needed for `-PullRequest` parameter or auto-detect mode
  - Verify: `gh auth status`
  - Install: `winget install GitHub.cli` (Windows) · `brew install gh` (macOS)
  - Docs: https://cli.github.com/

**Note**: This script uses PowerShell-specific features (e.g., `-notin` operator, `HashSet<T>`, `Invoke-RestMethod`) that cannot be easily replicated in bash. Always prefer `pwsh` over `powershell.exe` when both are available.

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `BuildId` | int | Yes (set 1) | - | Azure DevOps build ID to analyze |
| `PullRequest` | int | Yes (set 2) | - | GitHub PR number (resolves build ID via `gh pr checks`) |
| _(none)_ | - | - | - | Auto-detects PR for current git branch (default) |
| `IncludeLogs` | switch | No | - | Download task logs for failed tasks |
| `OutputPath` | string | No | `$env:TEMP` | Directory for JSON artifacts and logs |

**Note:** `BuildId`, `PullRequest`, and no-argument (current branch) are mutually exclusive parameter sets.

### Usage Examples

#### Auto-detect PR for Current Branch

```powershell
.\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1
```

Detects the PR for the current git branch via `gh pr view`, then analyzes its build.

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
| `FailureHierarchy` | object[] | Array of stage objects with nested jobs and tasks (see below) |
| `CanceledJobs` | string[] | Canceled job names |
| `TimedOutJobs` | string[] | Jobs canceled after ~60 min (format: "name (XX.X min)") |
| `CollateralCanceled` | string[] | Jobs canceled in < 5 min |
| `FailedTests` | string[] | Extracted test names (via regex patterns) |
| `BuildErrors` | string[] | Compilation errors and Nuke target exceptions |
| `ErrorMessages` | string[] | Raw error messages from failed tasks |
| `LogFiles` | string[] | Paths to downloaded log files |
| `ArtifactPath` | string | Directory containing saved JSON files |
| `BuildUrl` | string | Azure DevOps web URL for build |

#### `FailureHierarchy` Structure

Full hierarchy: Stage > Job > Task. Each element in the `FailureHierarchy` array is a stage object:

| Field | Type | Description |
|-------|------|-------------|
| `Name` | string | Stage name |
| `Result` | string | Stage result ("failed", "canceled", etc.) |
| `Jobs` | object[] | Array of job objects belonging to this stage |

Each job object has:

| Field | Type | Description |
|-------|------|-------------|
| `Name` | string | Job name |
| `Result` | string | Job result ("failed", "canceled") |
| `Duration` | string/null | Duration string for canceled jobs (e.g., "43.2 min"), null for failed jobs |
| `Tasks` | object[] | Array of failed task objects belonging to this job |

Each task object has:

| Field | Type | Description |
|-------|------|-------------|
| `Name` | string | Task name (e.g., "Run integration tests (Tracer)") |
| `Result` | string | Task result (always "failed") |

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

## Retry-AzureDevOpsFailedStages.ps1

**Location:** `tracer/tools/Retry-AzureDevOpsFailedStages.ps1`

**Purpose:** Retries failed or canceled stages in an Azure DevOps build via the REST API (PATCH to stages endpoint, API version 7.1).

### Prerequisites

Same as `Get-AzureDevOpsBuildAnalysis.ps1` (PowerShell 7+, Azure CLI, GitHub CLI for PR modes).

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `BuildId` | int | Yes (set 1) | - | Azure DevOps build ID |
| `PullRequest` | int | Yes (set 2) | - | GitHub PR number (resolves build ID via `gh pr checks`) |
| _(none)_ | - | - | - | Auto-detects PR for current git branch (default) |
| `All` | switch | No | - | Retry all failed/canceled stages without prompting |
| `Stage` | string[] | No | - | Retry specific stages by identifier |
| `ForceRetryAllJobs` | switch | No | - | Rerun all jobs in retried stages, not just failed ones |
| `WhatIf` | switch | No | - | Show what would be retried without actually retrying |
| `Confirm` | switch | No | - | Prompt for confirmation before each retry |

**Note:** `BuildId`, `PullRequest`, and no-argument (current branch) are mutually exclusive. `-All` and `-Stage` are mutually exclusive. If neither is provided, an interactive numbered prompt is shown.

### Usage Examples

#### Retry All Failed Stages

```powershell
.\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -All
```

#### Retry Specific Stages

```powershell
.\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -Stage integration_tests_linux, integration_tests_windows
```

#### Preview Without Retrying

```powershell
.\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -All -WhatIf
```

#### Retry via PR Number

```powershell
.\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -PullRequest 8172 -All
```

#### Force Rerun All Jobs

```powershell
.\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -All -ForceRetryAllJobs
```

### Output Object Shape

Returns an array of result objects to the pipeline (one per stage):

| Field | Type | Description |
|-------|------|-------------|
| `BuildId` | int | Azure DevOps build ID |
| `StageName` | string | Display name of the stage |
| `Identifier` | string | Stage identifier (used in API calls) |
| `Result` | string | `'RetryRequested'`, `'Failed'`, or `'Skipped'` |
| `Error` | string/null | Error message if retry failed, null on success |

### Behavior Notes

- Uses `SupportsShouldProcess` for `-WhatIf` and `-Confirm` support
- Per-stage error handling: a single stage failure doesn't abort remaining retries
- Interactive mode shows a numbered menu when neither `-All` nor `-Stage` is provided
- Non-interactive sessions (e.g., CI) require `-All` or `-Stage`

### Related Documentation

- [Troubleshooting CI Failures Skill](./SKILL.md) — AI agent skill using these scripts
- [CLAUDE.md](../../../AGENTS.md) — Windows CLI pitfalls and PowerShell conventions
