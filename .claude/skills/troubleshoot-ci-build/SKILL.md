---
name: troubleshoot-ci-build
description: Troubleshoot CI failures in dd-trace-dotnet Azure DevOps pipeline. Analyzes build failures, categorizes failures (infrastructure/flaky/real), and provides actionable recommendations. Use for PR failures or specific build IDs.
argument-hint: <pr NUMBER | build BUILD_ID>
disable-model-invocation: true
user-invocable: true
allowed-tools: WebFetch, Bash(gh pr checks:*), Bash(az devops invoke:*), Bash(az pipelines build list:*), Bash(az pipelines build show:*), Bash(az pipelines runs artifact list:*), Bash(az pipelines runs list:*), Bash(az pipelines runs show:*)
---

# Troubleshoot Azure DevOps Builds for dd-trace-dotnet

Troubleshoot Azure DevOps pipeline failures with automated analysis.

## Prerequisites

**CRITICAL**: This skill requires PowerShell to run the build analysis script.

**PowerShell version requirements**:
- **Recommended**: PowerShell 7+ (`pwsh`) - cross-platform, modern features
- **Minimum**: PowerShell 5.1 (`powershell.exe` on Windows only)

**Installation**:
- Windows: `winget install Microsoft.PowerShell` (or use built-in PowerShell 5.1)
- macOS: `brew install powershell/tap/powershell`
- Linux: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux

**If the user does not have PowerShell installed**:
1. Check for `pwsh` first: `pwsh -Version`
2. If not found and on Windows, check for PowerShell 5.1: `powershell -NoProfile -Command '$PSVersionTable.PSVersion'`
3. If neither found or version is too old, provide installation instructions from [README.md](README.md#installing-powershell)
4. Do NOT attempt to replicate the script functionality using bash/jq - the logic is too complex

**Always prefer `pwsh` over `powershell.exe`** when both are available (better cross-platform compatibility and modern features).

**Other requirements**:
- GitHub CLI (`gh`) authenticated (for PR analysis)
- Azure CLI (`az`) configured

## Additional Resources

- **[failure-patterns.md](failure-patterns.md)** - Reference guide with known CI failure patterns, categorization rules, and decision trees. Load when you need to categorize a failure type or compare against historical patterns.
- **[README.md](README.md)** - User-facing documentation with usage examples and installation instructions. Reference when explaining skill capabilities to users.
- **[scripts-reference.md](scripts-reference.md)** - Documentation for `Get-AzureDevOpsBuildAnalysis.ps1` script (parameters, usage, output structure).

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

**Important**: Use actual Unicode emoji characters (e.g., ❌, 🔴, 🟡, 🔵, 🔍, ✅) in output, NOT markdown emoji codes (e.g., `:x:`, `:red_circle:`). Markdown emoji codes are not rendered in all contexts.

### Phase 1: Quick Summary (Always Show This First)

```markdown
# CI Failure Analysis for Build <BUILD_ID>

**Status**: ❌ Failed
**Build**: [<BUILD_NUMBER>](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>)

**PR**: [#<PR_NUMBER>](https://github.com/DataDog/dd-trace-dotnet/pull/<PR_NUMBER>) _(if PR-triggered)_
**Branch**: `<source_branch_name>` _(use `triggerInfo["pr.sourceBranch"]` for PR builds instead of `sourceBranch`)_
**Commit**: `<commit_sha>`

## Quick Overview

**Failed Tasks** (<count>):
- `<task_name_1>`
- `<task_name_2>`
- `<task_name_3>`
...

**Failed Jobs** (<count> platforms affected):
- `<job_name_1>` (e.g., Test alpine_net8.0_Tracer)
- `<job_name_2>` (e.g., Win x86_net8.0_Tracer)
...

**Failed Stages**:
- integration_tests_linux
- integration_tests_windows
...

**Timed Out Jobs** (<count>, canceled after ~60 min):
- `DockerTest alpine_netcoreapp3.0_group1 (60.3 min)`
- `IntegrationTests Windows x64 net8.0 (58.7 min)`
...
_(Note: These are jobs with result="canceled" and duration >= 55 minutes)_

**Collateral Cancellations** (<count>, < 5 min):
- `Dependent Job 1`
- `Dependent Job 2`
...
_(Note: Jobs canceled quickly, likely due to parent stage failure)_

**Failed Tests** (<count>, if applicable):
- `TestNamespace.TestClass.TestMethod1`
- `TestNamespace.TestClass.TestMethod2`
- `TestNamespace.TestClass.TestMethod3`
...
_(Note: List specific test names extracted from error messages, if test failures detected)_

**Snapshot Mismatches Detected** _(if applicable — show only when snapshot failures are detected)_

The following failed tests likely involve snapshot verification:
- `TestClass.SubmitsTraces`
- ...

To update snapshots from this build:
- **Windows**: `./tracer/build.ps1 UpdateSnapshotsFromBuild --BuildId <BUILD_ID>`
- **Linux/macOS**: `./tracer/build.sh UpdateSnapshotsFromBuild --BuildId <BUILD_ID>`

This downloads the `.received.txt` snapshot artifacts from CI and replaces your local `.verified.txt` files.

---

## 🔍 What would you like to investigate?

1. **Categorize failures** - Analyze failure types (infrastructure/flaky/real)
2. **View specific logs** - Download logs for failed tasks
3. **Show full analysis** - Run complete analysis with all details
4. **Update snapshots** - Download and apply updated snapshots from this build _(shown only when snapshot failures detected)_
```

**Resolving PR number**: When invoked with `build <BUILD_ID>`, extract the PR number from the build details JSON:
- `triggerInfo["pr.number"]` — most direct
- `sourceBranch` — parse from `refs/pull/<NUMBER>/merge` pattern
- Only show the PR link if the build was PR-triggered (`reason == "pullRequest"`)


### Phase 2: Detailed Output (Only If User Requests)

**If user requests log analysis**:
```markdown
## Log Analysis

Attempted to download logs for failed tasks:

✅ Successfully retrieved:
- Task: `<name>` (Log ID: <id>)
  - Error pattern: <brief description>

❌ Failed to retrieve (HTTP 500):
- Task: `<name>` (Log ID: <id>)
  - View manually: [Link](https://...)
```

**If user requests categorization**:
```markdown
## Failure Categories

### 🔴 Real Failures (Action: Investigate)
- `<task>` - <reason>

### 🟡 Flaky Tests (Action: Consider Retry)
- `<task>` - <reason>

### 🔵 Infrastructure Issues (Action: Retry)
- `<task>` - <reason>
```

## Failure Categorization Rules

### 🔴 Real Failures (Action: Investigate)
**Indicators**:
- Test assertions: `Expected X but got Y`, `Assert.*failed`
- Compilation errors: `error CS\d+`, `MSB\d+`
- Segmentation faults: `SIGSEGV`, `Access Violation`
- Missing spans: `Expected N spans but got M`

**Pattern examples**:
```
[FAIL] TestName
Expected 21 spans but got 14
Assert.Equal() Failure
Expected: True
Actual:   False
```

### 🟡 Flaky Tests (Action: Retry, May Investigate)
**Indicators**:
- Tests with `previousAttempts > 0` (already auto-retried by CI)
- Stack walking failures: `Failed to walk N stacks for sampled exception: E_FAIL`
- Known intermittent tests (reference `failure-patterns.md`)
- Tests that pass/fail inconsistently across platforms
- **Single-runtime failures**: Same test fails on only one .NET runtime but passes on others (especially net6+). Example: net6 pass, net8 pass, net10 fail → likely flaky, not a real regression.
- **ARM64 single-platform timeout**: In an ARM64 stage (e.g., `unit_tests_arm64`), one runtime job is cancelled after ~60 min while all other runtimes complete normally in ~14 min → almost certainly a transient ARM64 infrastructure issue, not a code regression. Retry.

**Pattern examples**:
```
Stack walking failed with E_FAIL
ThreadAbortException
Timeout waiting for spans
```

### 🔵 Infrastructure Failures (Action: Retry)
**Indicators**:
- Docker rate limiting: `toomanyrequests`, `pull rate limit exceeded`
- Network timeouts: `TLS handshake timeout`, `Connection reset by peer`, `ECONNRESET`
- Execution timeouts: `maximum execution time exceeded`, `Test timeout`
- Timeout via cancellation: Job canceled with duration >= 55 minutes
- Disk space: `No space left on device`, `ENOSPC`
- Container failures: `docker: Error response from daemon`

**Pattern examples**:
```
pull access denied, repository does not exist or may require authentication
TLS handshake timeout
Connection reset by peer
Build failed in XX:XX:XX (timeout)
```

**Recommendation**: Retry the build once. If the failure persists after 2 consecutive runs, investigate and alert the **#apm-dotnet** Slack channel.

## API Reference

### Azure DevOps REST API

**Base URL**: `https://dev.azure.com/datadoghq`
**Project**: `dd-trace-dotnet`
**Project ID**: `a51c4863-3eb4-4c5d-878a-58b41a049e4e`

**Using az devops invoke**:
```bash
az devops invoke \
  --area <area> \
  --resource <resource> \
  --route-parameters project=dd-trace-dotnet [key=value ...] \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  [--query-parameters key=value ...]
```

**Common endpoints**:

**Get Build Details**:
```bash
az devops invoke --area build --resource builds \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID
```

**Get Build Timeline**:
```bash
az devops invoke --area build --resource timeline \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID
```

**Get Build Logs** (may fail with HTTP 500):
```bash
az devops invoke --area build --resource logs \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID logId=$LOG_ID
```

### GitHub CLI

**Get PR Checks**:
```bash
gh pr checks <PR_NUMBER> --repo DataDog/dd-trace-dotnet \
  --json name,state,link,completedAt
```

Available fields: `bucket`, `completedAt`, `description`, `event`, `link`, `name`, `startedAt`, `state`, `workflow`

## Windows CLI Pitfalls (Lessons Learned)

### 0. Always Use Scratchpad Directory

**Problem**: Using `/tmp` or `$TEMP` can cause issues on Windows

**Solution**: Always use the scratchpad directory provided in the system prompt

```bash
# ❌ BAD - Don't hardcode /tmp
az devops invoke ... > /tmp/timeline.json

# ✅ GOOD - Use scratchpad from system prompt
SCRATCHPAD="<path provided in system prompt>"
az devops invoke ... > "$SCRATCHPAD/timeline.json"
```

### 1. Complex jq Filters Fail on Windows

**Problem**: Complex jq expressions with `!=` or nested filters cause parse errors

**Solution**: Save JSON to file first, then query with simpler filters

```bash
# ❌ BAD - Complex filter inline (fails on Windows)
az devops invoke ... | jq '.records[] | select(.issues != null and .issues != [])'

# ✅ GOOD - Save first, then query
az devops invoke ... --output json > "$SCRATCHPAD/timeline.json"
cat "$SCRATCHPAD/timeline.json" | jq '.records[] | select(.issues)'
```

### 2. API 500 Errors for Logs - USE TIMELINE URLs INSTEAD

**Problem**: `az devops invoke --resource logs` frequently returns HTTP 500

**Solution**: Use the log URLs directly from the timeline data instead of the logs API

```bash
# ❌ BAD - az devops invoke --resource logs returns HTTP 500
az devops invoke --area build --resource logs \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID logId=$LOG_ID

# ✅ GOOD - Extract log URL from timeline and use curl
LOG_URL=$(cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .log.url' | head -1)
curl -s "$LOG_URL" > "$SCRATCHPAD/build-$BUILD_ID-log.txt"

# ✅ GOOD - Or use WebFetch tool
LOG_URL=$(cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .log.url' | head -1)
# Then use WebFetch with $LOG_URL
```

The timeline `.log.url` field provides direct URLs that work reliably:
```
https://dev.azure.com/datadoghq/<project-id>/_apis/build/builds/<buildId>/logs/<logId>
```

### 3. Piping to head Causes Errors

**Problem**: `jq ... | head -50` can cause "Invalid argument" errors on Windows

**Solution**: Avoid piping jq to head entirely; let jq output naturally or use first()

```bash
# ❌ BAD - Causes "Invalid argument" on Windows
jq '.records[]' | head -50

# ✅ GOOD - Let jq output naturally (it's filtered already)
jq '.records[] | select(.result == "failed")'

# ✅ GOOD - Use first() if you only need one result
jq '.records[] | select(.result == "failed") | first'

# ✅ GOOD - Use limit if you need specific count
jq '[.records[] | select(.result == "failed")] | .[0:10]'
```

### 4. Query Parameter Escaping

**Problem**: Special characters in query parameters need escaping on Windows

**Solution**: Use single quotes for parameter values containing special chars

```bash
# ❌ BAD - $top interpreted as shell variable
--query-parameters branchName=refs/heads/master $top=5

# ✅ GOOD - Quote the parameter
--query-parameters branchName=refs/heads/master '$top=10'
```

### 5. `--route-parameters` Must Be Separate Arguments

**Problem**: Passing route parameters as a single space-separated string causes cryptic authentication errors (`TF400813: The user '...' is not authorized`)

**Solution**: Each key=value pair must be a separate argument

```bash
# ❌ BAD - Single string, causes auth errors
az devops invoke --route-parameters "project=dd-trace-dotnet buildId=12345"

# ✅ GOOD - Separate arguments
az devops invoke --route-parameters project=dd-trace-dotnet buildId=12345
```

In PowerShell, when building argument arrays, split them into individual elements:
```powershell
# ❌ BAD - Single array element
$azArgs += "project=dd-trace-dotnet buildId=$BuildId"

# ✅ GOOD - Separate array elements
$azArgs += "project=dd-trace-dotnet"
$azArgs += "buildId=$BuildId"
```

### 6. jq Pitfalls with Nullable Fields

**Problem**: Using `startswith()`, `contains()`, or `test()` on nullable fields causes errors when the field is null

**Solution**: Guard with `!= null` or use `// ""` default value

```bash
# ❌ BAD - Fails if .name is null
jq '.records[] | select(.name | startswith("Test"))'

# ✅ GOOD - Guard with null check
jq '.records[] | select(.name != null and (.name | startswith("Test")))'

# ✅ GOOD - Use default value
jq '.records[] | select((.name // "") | startswith("Test"))'
```

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

## Supporting Files

Additional resources in this skill directory:

- **[failure-patterns.md](failure-patterns.md)** - Known failure patterns and their classifications
- **[README.md](README.md)** - User-facing documentation and examples

Reference these files when categorizing failures or looking for known issues.

## Examples

### Example 1: Initial Quick Analysis

**Command**: `/troubleshoot-ci-build build 195272`

**Phase 1 Output** (shown immediately):
```
# CI Failure Analysis for Build 195272

**Build**: 20260204-49
**Status**: ❌ Failed
**Failed Tasks**: 8
  - RunWindowsIisTracerIntegrationTests (2 occurrences)
  - docker-compose run IntegrationTests (Tracer) (2 occurrences)
  - Run integration tests (Tracer) (2 occurrences)
  - docker-compose run --no-deps IntegrationTests (2 occurrences)

**Failed Jobs**: 8 platforms
  - Test alpine_net8.0_Tracer
  - Test debian_net8.0_Tracer
  - Win x86_net8.0_Tracer
  - Win x64_net8.0_Tracer
  (and 4 more...)

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
```

### Example 2: PR Analysis

**Command**: `/troubleshoot-ci-build pr 7806`

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

## Key Learnings & Best Practices

### Timeline Record Structure

The Azure DevOps timeline contains a hierarchy:
- **Stage** → **Phase** → **Job** → **Task**
- Filter by `type` field: `"Stage"`, `"Phase"`, `"Job"`, or `"Task"`
- Failed stages/jobs cascade down - focus on Task-level failures for specifics
- Job `identifier` field reveals platform/variant info (e.g., `integration_tests_linux.Test.Job23`)

### Canceled Jobs and Timeout Detection

**Result values**: `"succeeded"`, `"failed"`, `"canceled"`, `"abandoned"`

**Canceled vs Failed**: Jobs canceled due to timeout have `result == "canceled"`, NOT `"failed"`.

**Duration-based Classification**:
- **Timeout** (>= 55 min): Job likely hit Azure DevOps 60-minute timeout
- **Collateral** (< 5 min): Job canceled quickly due to parent stage failure
- **Unknown** (5-55 min): Could be manual cancellation or other cause

**Key Rule**: If a stage shows `result == "failed"` but no jobs have `result == "failed"`, check for canceled jobs with duration >= 55 minutes. These are likely timeouts that caused the stage failure.

**Example**: Build 195486 - `integration_tests_linux` stage failed with no failed jobs, but `DockerTest alpine_netcoreapp3.0_group1` was canceled after 60.3 minutes (timeout).

### Understanding Test Results

1. **Build warnings vs test failures**:
   - `.issues` array contains warnings (build warnings, package warnings)
   - Check `.result == "failed"` for actual failures
   - Don't confuse build warnings with test failures

2. **Log download failures**:
   - Azure DevOps logs API frequently returns HTTP 500
   - This is a known limitation, not a user error
   - Always provide web UI links as fallback
   - The timeline itself contains valuable data even without logs

## Notes

- This skill always runs with `disable-model-invocation: true` - must be invoked manually
- Requires `gh` CLI authenticated for PR analysis
- Requires `az` CLI configured (but public API, no auth needed for dd-trace-dotnet)
- Large builds may take 30-60 seconds to analyze
- Log downloads are best-effort; may fail due to Azure DevOps API issues
- Always use scratchpad directory from system prompt, never hardcode /tmp paths
