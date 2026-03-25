---
name: analyze-azdo-build
description: Analyze Azure DevOps CI build failures in dd-trace-dotnet pipeline. Use this skill whenever the user mentions a failing CI build, PR checks failing, Azure DevOps pipeline failures, test failures in CI, or when they share a build ID or PR number and want to understand what went wrong. Analyzes build failures, categorizes them (infrastructure/flaky/real), and provides actionable recommendations.
argument-hint: <pr NUMBER | build BUILD_ID>
user-invocable: true
allowed-tools: WebFetch, Bash(pwsh -Version*), Bash(pwsh *Get-AzureDevOpsBuildAnalysis.ps1*), Bash(pwsh *Retry-AzureDevOpsFailedStages.ps1*), Bash(gh pr checks:*), Bash(az account show*), Bash(az account set*), Bash(az devops invoke:*), Bash(az pipelines build list:*), Bash(az pipelines build show:*), Bash(az pipelines runs artifact list:*), Bash(az pipelines runs list:*), Bash(az pipelines runs show:*)
---

# Troubleshoot Azure DevOps Builds for dd-trace-dotnet

Troubleshoot Azure DevOps pipeline failures with automated analysis.

## Prerequisites

This skill requires the following tools. If any are missing, provide the relevant install instructions below.

### PowerShell 7+ (`pwsh`) — Required

The build analysis script requires PowerShell. PowerShell 7+ (`pwsh`) is recommended; PowerShell 5.1 (`powershell.exe`, Windows only) is the minimum.

- **Verify**: `pwsh -Version`
- **Install**: `winget install Microsoft.PowerShell` (Windows) · `brew install powershell/tap/powershell` (macOS)
- **Docs**: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell

**If the user does not have PowerShell installed**:
1. Check for `pwsh` first: `pwsh -Version`
2. If not found and on Windows, check for PowerShell 5.1: `powershell -NoProfile -Command '$PSVersionTable.PSVersion'`
3. If neither found or version is too old, provide installation instructions above
4. Do NOT attempt to replicate the script functionality using bash/jq - the logic is too complex

**Always prefer `pwsh` over `powershell.exe`** when both are available.

### GitHub CLI (`gh`) — Required for PR analysis

Used to resolve PR numbers to Azure DevOps build IDs. Must be authenticated.

- **Verify**: `gh auth status`
- **Install**: `winget install GitHub.cli` (Windows) · `brew install gh` (macOS)
- **Docs**: https://cli.github.com/

### Azure CLI (`az`) with `azure-devops` extension — Required

Used by the PowerShell script to query Azure DevOps build and timeline APIs.

- **Verify**: `az version` (check that `azure-devops` appears under "extensions")
- **Install CLI**: `winget install Microsoft.AzureCLI` (Windows) · `brew install azure-cli` (macOS)
- **Install extension**: `az extension add --name azure-devops`
- **Docs**: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli

## Additional Resources

- **[failure-patterns.md](failure-patterns.md)** — Load ONLY during Phase 2 categorization or when the user asks about a specific failure type. Not needed for Phase 1 quick analysis.
- **[scripts-reference.md](scripts-reference.md)** — Load ONLY if the PowerShell script fails, returns unexpected output, or you need to understand the output object shape.
- **[references/cli-reference.md](references/cli-reference.md)** — Load ONLY if bypassing the PowerShell script entirely and running Azure DevOps CLI commands directly.

## Task

You are analyzing CI failures for the dd-trace-dotnet repository.

**Phase 1 - Quick Initial Analysis (DO THIS FIRST)**:
1. **Fetch build details** - Get build status, branch, commit
2. **Identify failed tasks** - List which tasks/jobs failed
3. **Show quick summary** - Present overview to user
4. **Ask user what to investigate** - Prompt for next steps

**Assumption**: All tests pass in master. If a test were consistently failing, it wouldn't have been merged.

**Phase 2 - Deep Analysis (ONLY IF USER REQUESTS)**:
- Download and analyze logs (if available)
- Categorize failures (infrastructure/flaky/real)
- Provide detailed recommendations

## Arguments

The skill accepts these invocation patterns:

- **`pr <NUMBER>`** - Analyze failures for a GitHub PR
- **`build <BUILD_ID>`** - Analyze a specific Azure DevOps build
- **No arguments** - The script auto-detects the PR for the current git branch

Arguments are available as: `$ARGUMENTS`

## Implementation Steps

### PHASE 1: Quick Initial Analysis

Perform these steps quickly to give user an overview, then ask what they want to investigate.

#### Step 1-4: Run Quick Analysis Script

Use the `Get-AzureDevOpsBuildAnalysis.ps1` script for quick analysis:

**No arguments provided** — the script auto-detects the PR for the current branch:
```bash
pwsh -NoProfile -Command ".\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -Verbose"
```

**For PR analysis** (`pr <NUMBER>`):
```bash
pwsh -NoProfile -Command ".\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -PullRequest $PR_NUMBER -Verbose"
```

**For direct build analysis** (`build <BUILD_ID>`):
```bash
pwsh -NoProfile -Command ".\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId $BUILD_ID -Verbose"
```

The script outputs:
- Build summary (ID, number, status, result, branch, commit)
- Failed stages, jobs, and tasks
- Extracted failed test names (via regex patterns)
- Saved artifacts (JSON files in temp directory)

**Note**: The script uses the scratchpad/temp directory automatically for saved artifacts.

#### Step 5: Present Quick Summary & Ask User

Present a concise summary and ask what they want to investigate next.

#### Snapshot Mismatch Detection

After presenting the quick summary, check if any failed tests are likely snapshot verification failures. Detect these by looking for:

- **Error messages** containing `Received file does not match`, `*.received.*` vs `*.verified.*` diffs, or Verify assertion failures
- **Test names** containing `SubmitsTraces` in `Datadog.Trace.ClrProfiler.IntegrationTests.*` — but **only if** the error is a snapshot diff, not a span count mismatch

**Important**: `SubmitsTraces` tests typically assert on span count first (`Expected N spans but got M`), then compare snapshots. A span count mismatch is a deeper issue (missing/extra instrumentation), not a snapshot problem — updating snapshots won't help. Only suggest snapshot updates when the error is specifically about snapshot file content differences.

If snapshot failures are detected:

1. Include the **"Snapshot Mismatches Detected"** block in the Phase 1 output (see Output Format below)
2. Add the **"Update snapshots"** option to the investigation menu
3. If the user chooses to update snapshots, run:
   - **Windows**: `./tracer/build.ps1 UpdateSnapshotsFromBuild --BuildId <BUILD_ID>`
   - **Linux/macOS**: `./tracer/build.sh UpdateSnapshotsFromBuild --BuildId <BUILD_ID>`
   - This downloads `.received.txt` snapshot artifacts from the CI build and replaces local `.verified.txt` files in `tracer/test/snapshots/`
   - **Prerequisite**: The build must have run far enough to produce snapshot artifacts (even if tests failed)
   - If the changes are unintentional, the developer should investigate the code change instead of updating snapshots

### Retry Failed Stages (Only If User Requests)

When the user selects "Retry failed stages" from the investigation menu:

1. **Verify Azure account** — Before retrying, ensure the correct Azure subscription is active:
   ```bash
   az account show --query "{name:name, id:id}" -o table
   ```
   Run `az account list -o table` to find the correct subscription if needed, then switch:
   ```bash
   az account set --subscription <subscription-id>
   ```

   Do NOT proceed with the retry until the correct account is confirmed.

3. **Show what would be retried** — Run with `-WhatIf` first to preview:
   ```bash
   pwsh -NoProfile -Command ".\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId $BUILD_ID -All -WhatIf"
   ```

4. **Confirm with user** — Show the list of stages that would be retried and ask for confirmation.

5. **Run the retry** — After confirmation:
   ```bash
   pwsh -NoProfile -Command ".\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId $BUILD_ID -All"
   ```

   Or for specific stages:
   ```bash
   pwsh -NoProfile -Command ".\tracer\tools\Retry-AzureDevOpsFailedStages.ps1 -BuildId $BUILD_ID -Stage stage_identifier_1, stage_identifier_2"
   ```

6. **Optionally re-check status** — After retrying, offer to re-run the analysis script later to check the new results.

**Note**: Use `-ForceRetryAllJobs` if the user wants to rerun all jobs in a stage, not just the failed ones.

### PHASE 2: Deep Analysis (Only If Requested)

**DO NOT perform these steps automatically**. Only do them if the user asks for:
- Log analysis
- Detailed categorization
- Specific failure investigation

#### Deep Analysis Steps

**Download Logs** (if requested):

```bash
pwsh -NoProfile -Command ".\tracer\tools\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId $BUILD_ID -IncludeLogs -Verbose"
```

The script automatically:
- Extracts log URLs from timeline records (not the broken logs API)
- Downloads all failed task logs via `Invoke-RestMethod`
- Handles failures gracefully (non-fatal warnings)
- Reports downloaded log file paths

**Note**: Log downloads use timeline URLs directly, not `az devops invoke --resource logs` which returns HTTP 500.

## Output Format

**Important**: Use actual Unicode emoji characters (e.g., ❌, 🔴, 🟡, 🔵, 🔍, ✅), NOT markdown emoji codes (e.g., `:x:`).

### Phase 1: Quick Summary

Structure the output as:

1. **Header**: `# CI Failure Analysis for Build <BUILD_ID>`
2. **Metadata**: Status, Build link (`https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>`), PR link (if PR-triggered), Branch, Commit
   - For PR builds, use `triggerInfo["pr.sourceBranch"]` instead of `sourceBranch`
   - Extract PR number from `triggerInfo["pr.number"]` or parse from `refs/pull/<NUMBER>/merge`
3. **Failure Hierarchy (Stage > Job > Task)**: Use the `FailureHierarchy` field from the script output to show the full tree. Format as:
   ```
   ❌ stage_name
       ❌ failed_job_name
           - failed_task_name
       ⚠️ canceled_job_name (duration)
   ```
   Use ❌ for failed, ⚠️ for canceled stages/jobs. Tasks are listed with `- ` prefix under their job.
4. **Timed Out Jobs**: Jobs with `result="canceled"` and duration >= 55 min (show duration)
5. **Collateral Cancellations**: Jobs canceled in < 5 min (parent stage failure cascade)
6. **Failed Tests**: Specific test names extracted from error messages
7. **Snapshot Mismatches** (if detected): List affected tests and show `UpdateSnapshotsFromBuild` command
8. **Investigation menu**: Categorize failures / View logs / Full analysis / Retry failed stages / Update snapshots (if applicable)

### Phase 2: Detailed Output (Only If User Requests)

- **Log analysis**: List successfully retrieved vs failed downloads with error patterns
- **Categorization**: Group failures into 🔴 Real / 🟡 Flaky / 🔵 Infrastructure (see [failure-patterns.md](failure-patterns.md))

## Failure Categorization

For detailed categorization rules, pattern examples, and the decision tree, see [failure-patterns.md](failure-patterns.md).

**Quick reference** — three categories:
- 🔴 **Real Failures** — Test assertions, compilation errors, segfaults, span count mismatches → Investigate
- 🟡 **Flaky Tests** — Auto-retried tests, single-runtime failures, Alpine stack walking, ARM64 timeouts → Retry
- 🔵 **Infrastructure** — Docker rate limits, network timeouts, disk space, job cancellation >= 55 min → Retry

## Error Handling

### Build Not Found
- Check if PR has CI runs: `gh pr checks <PR> --repo DataDog/dd-trace-dotnet`
- Verify build ID is correct
- Check if build is still queued (not completed)

### Logs Too Large or Unavailable
- **Preferred method**: Extract log URLs from timeline (`.log.url` field) and use `curl` or `WebFetch` to download
- The `az devops invoke --resource logs` API returns HTTP 500, so avoid it
- If curl/WebFetch fails, provide Azure DevOps web UI link for manual inspection
- Check `.issues` field in timeline records for inline error messages (already available without downloading logs)
- Build warnings in `.issues` are not the same as test failures - filter by `result == "failed"`

### API Rate Limiting
- Wait and retry after delay
- Cache timeline data to temporary files
- Suggest user check web UI if repeated failures

## Examples

### Example 1: Initial Quick Analysis

**Command**: `/analyze-azdo-build build 195272`

**Phase 1 Output** (shown immediately):
```
# CI Failure Analysis for Build 195272

**Build**: 20260204-49
**Status**: ❌ Failed

**Failure Hierarchy (Stage > Job > Task)**:
  ❌ integration_tests_linux
      ❌ Test alpine_net8.0_Tracer
          - docker-compose run IntegrationTests (Tracer)
      ❌ Test debian_net8.0_Tracer
          - docker-compose run IntegrationTests (Tracer)
  ❌ integration_tests_windows
      ❌ Win x86_net8.0_Tracer
          - Run integration tests (Tracer)
      ❌ Win x64_net8.0_Tracer
          - Run integration tests (Tracer)
      (and 4 more...)
  ⚠️ profiler_integration_tests
      ⚠️ Test alpine (43.2 min)

**Failed Tests**: 12
  - Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore.AspNetCoreMvcTests.SubmitMetrics
  - Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore.AspNetCoreMvcTests.TracingDisabled_DoesNotSubmitTraces
  - Datadog.Trace.ClrProfiler.IntegrationTests.HttpClientTests.HttpClient_GetAsync_SubmitsTraces
  - Datadog.Trace.ClrProfiler.IntegrationTests.HttpClientTests.HttpClient_PostAsync_SubmitsTraces
  (and 8 more...)

What would you like to investigate?
1. Categorize failures
2. View specific logs
3. Show full analysis
4. Retry failed stages
```

### Example 2: PR Analysis

**Command**: `/analyze-azdo-build pr 7806`

**Output**:
```
Found Azure DevOps build 195272 for PR #7806

[Quick summary as in Example 1]

What would you like to investigate?
1. Categorize failures
2. View specific logs
3. Show full analysis
```

## Related Documentation

- **CI Troubleshooting Guide**: `docs/development/CI/TroubleshootingCIFailures.md` - Manual troubleshooting steps
- **Run Tests Locally**: `docs/development/CI/RunSmokeTestsLocally.md` - Reproduce failures locally
- **Azure DevOps Pipeline**: `.azure-pipelines.yml` - Pipeline configuration

### Timeline Record Structure

The Azure DevOps timeline contains a hierarchy:
- **Stage** → **Phase** → **Job** → **Task**
- Filter by `type` field: `"Stage"`, `"Phase"`, `"Job"`, or `"Task"`
- Failed stages/jobs cascade down - focus on Task-level failures for specifics
- Job `identifier` field reveals platform/variant info (e.g., `integration_tests_linux.Test.Job23`)
- Result values: `"succeeded"`, `"failed"`, `"canceled"`, `"abandoned"`
- Jobs canceled due to timeout have `result == "canceled"`, NOT `"failed"` — see `failure-patterns.md` for classification details

## Notes

- Requires `gh` CLI authenticated for PR analysis (see Prerequisites)
- Requires `az` CLI with `azure-devops` extension (see Prerequisites)
- Large builds may take 30-60 seconds to analyze
- Log downloads are best-effort; may fail due to Azure DevOps API issues
- Always use scratchpad directory from system prompt, never hardcode /tmp paths
