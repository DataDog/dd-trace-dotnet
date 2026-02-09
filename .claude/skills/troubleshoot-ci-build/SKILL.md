---
name: troubleshoot-ci-build
description: Troubleshoot CI failures in dd-trace-dotnet Azure DevOps pipeline. Analyzes build failures, compares with master builds, categorizes failures (infrastructure/flaky/real), and provides actionable recommendations. Use for PR failures or specific build IDs.
argument-hint: <pr NUMBER | build BUILD_ID | compare BUILD_ID BASELINE_ID>
disable-model-invocation: true
user-invocable: true
allowed-tools: WebFetch, Bash(az devops invoke:*), Bash(az pipelines build list:*), Bash(az pipelines build show:*), Bash(az pipelines runs artifact list:*), Bash(az pipelines runs list:*), Bash(az pipelines runs show:*)
---

# Troubleshoot Azure DevOps Builds for dd-trace-dotnet

Troubleshoot Azure DevOps pipeline failures with automated analysis and comparison against master builds.

## Additional Resources

- **[failure-patterns.md](failure-patterns.md)** - Reference guide with known CI failure patterns, categorization rules, and decision trees. Load when you need to categorize a failure type or compare against historical patterns.
- **[README.md](README.md)** - User-facing documentation with usage examples. Reference when explaining skill capabilities to users.

## Task

You are analyzing CI failures for the dd-trace-dotnet repository.

**Phase 1 - Quick Initial Analysis (DO THIS FIRST)**:
1. **Fetch build details** - Get build status, branch, commit
2. **Identify failed tasks** - List which tasks/jobs failed
3. **Show quick summary** - Present overview to user
4. **Ask user what to investigate** - Prompt for next steps

**Phase 2 - Deep Analysis (ONLY IF USER REQUESTS)**:
- Compare with master builds
- Download and analyze logs (if available)
- Categorize failures (infrastructure/flaky/real)
- Provide detailed recommendations

## Arguments

The skill accepts three invocation patterns:

- **`pr <NUMBER>`** - Analyze failures for a GitHub PR
- **`build <BUILD_ID>`** - Analyze a specific Azure DevOps build
- **`compare <BUILD_ID> <BASELINE_ID>`** - Compare two builds directly

Arguments are available as: `$ARGUMENTS`

## Implementation Steps

### PHASE 1: Quick Initial Analysis

Perform these steps quickly to give user an overview, then ask what they want to investigate.

#### Step 1: Identify the Build

**For PR analysis** (`pr <NUMBER>`):
```bash
gh pr checks $0 --repo DataDog/dd-trace-dotnet --json name,state,link,completedAt
```

Extract the Azure DevOps build ID from `link`. The URL format is:
```
https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>
```

Note: The `state` field contains values like "SUCCESS", "FAILURE", "PENDING", "SKIPPED".

**For direct build analysis** (`build <BUILD_ID>`):
Use the provided build ID directly.

#### Step 2: Fetch Build Details

Get basic build information:

```bash
# Use scratchpad directory from system prompt for temporary files

az devops invoke \
  --area build \
  --resource builds \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  --output json > "<scratchpad-path>/build-$BUILD_ID-details.json"

cat "<scratchpad-path>/build-$BUILD_ID-details.json" | \
  jq '{id, buildNumber, status, result, sourceBranch, sourceVersion, finishTime}'
```

#### Step 3: Fetch Build Timeline

Save timeline to file for analysis:

```bash
# Use scratchpad directory from system prompt for temporary files

az devops invoke \
  --area build \
  --resource timeline \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  --output json > "<scratchpad-path>/build-$BUILD_ID-timeline.json"
```

#### Step 4: Extract Failed Tasks (Quick Summary Only)

Get a quick count and list of failures:

```bash
# Count failed tasks
cat "<scratchpad-path>/build-$BUILD_ID-timeline.json" | \
  jq '[.records[] | select(.result == "failed" and .type == "Task")] | length'

# Get unique failed task names
cat "<scratchpad-path>/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .name' | \
  sort | uniq

# Get failed jobs (shows platforms/variants affected)
cat "<scratchpad-path>/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Job") | .name'
```

#### Step 4.5: Extract Specific Failed Tests (If Test Failures)

If the failed tasks are test-related (names containing "test", "IntegrationTests", etc.), extract specific test names from error messages:

```bash
# Check if failures are test-related (inspect task names)
cat "<scratchpad-path>/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .name' | \
  grep -iE "(test|integration)" && echo "Test failures detected"

# Extract error messages from failed task issues
cat "<scratchpad-path>/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .issues[]? | .message' \
  > "<scratchpad-path>/build-$BUILD_ID-error-messages.txt"

# Parse test names from error messages
# Look for patterns like:
# - [FAIL] TestName
# - Failed TestNamespace.TestClass.TestMethod
# - Assert.* Failure in TestName
grep -oE '\[FAIL\] [A-Za-z0-9_.]+\.[A-Za-z0-9_]+' "<scratchpad-path>/build-$BUILD_ID-error-messages.txt" | \
  sed 's/\[FAIL\] //' | sort | uniq > "<scratchpad-path>/build-$BUILD_ID-failed-tests.txt"

# Alternative pattern: "Failed TestNamespace.TestClass.TestMethod"
grep -oE 'Failed [A-Za-z0-9_.]+\.[A-Za-z0-9_]+' "<scratchpad-path>/build-$BUILD_ID-error-messages.txt" | \
  sed 's/Failed //' | sort | uniq >> "<scratchpad-path>/build-$BUILD_ID-failed-tests.txt"

# Deduplicate and display
sort -u "<scratchpad-path>/build-$BUILD_ID-failed-tests.txt"
```

**Note**: Error message parsing is best-effort. If patterns don't match, show the raw error messages instead.

#### Step 5: Present Quick Summary & Ask User

Present a concise summary and ask what they want to investigate next.

### PHASE 2: Deep Analysis (Only If Requested)

**DO NOT perform these steps automatically**. Only do them if the user asks for:
- Comparison with master
- Log analysis
- Detailed categorization
- Specific failure investigation

#### Deep Analysis Steps

**Compare with Master** (if requested):

```bash
# Find recent master builds
az devops invoke \
  --area build \
  --resource builds \
  --route-parameters project=dd-trace-dotnet \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  --query-parameters branchName=refs/heads/master '$top=10' \
  --output json > "$SCRATCHPAD/master-builds.json"

# Get most recent succeeded build
cat "$SCRATCHPAD/master-builds.json" | \
  jq -r '.value[] | select(.result == "succeeded") | {id, buildNumber, finishTime}' | head -1

# Fetch master timeline
az devops invoke \
  --area build \
  --resource timeline \
  --route-parameters project=dd-trace-dotnet buildId=$MASTER_BUILD_ID \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  --output json > "$SCRATCHPAD/build-$MASTER_BUILD_ID-timeline.json"

# Compare failures
cat "$SCRATCHPAD/build-$MASTER_BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .name' | \
  sort | uniq > "$SCRATCHPAD/master-failures.txt"

cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .name' | \
  sort | uniq > "$SCRATCHPAD/pr-failures.txt"

# New failures (in PR but not in master)
comm -13 "$SCRATCHPAD/master-failures.txt" "$SCRATCHPAD/pr-failures.txt"
```

**Download Logs** (if requested):

The timeline data includes direct URLs to logs. Use these URLs instead of the Azure DevOps logs API (which returns HTTP 500):

```bash
# Extract log URLs for failed tasks
cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | {name, log_id: .log.id, log_url: .log.url}'

# Download a specific log by URL (using curl or WebFetch)
LOG_URL=$(cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .log.url' | head -1)

curl -s "$LOG_URL" > "$SCRATCHPAD/build-$BUILD_ID-log.txt"

# Or download all failed task logs
cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .log.url' | \
  while read -r url; do
    log_id=$(basename "$url")
    curl -s "$url" > "$SCRATCHPAD/build-$BUILD_ID-log-$log_id.txt"
    echo "Downloaded log $log_id"
  done
```

**Note**: The log URLs from the timeline work reliably, unlike `az devops invoke --resource logs` which frequently returns HTTP 500.

## Output Format

### Phase 1: Quick Summary (Always Show This First)

```markdown
# CI Failure Analysis for Build <BUILD_ID>

**Build**: [<BUILD_NUMBER>](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>)
**Status**: ❌ Failed
**Branch**: `<branch_name>`
**Commit**: `<commit_sha>`
**Finished**: <timestamp>

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

**Failed Tests** (<count>, if applicable):
- `TestNamespace.TestClass.TestMethod1`
- `TestNamespace.TestClass.TestMethod2`
- `TestNamespace.TestClass.TestMethod3`
...
_(Note: List specific test names extracted from error messages, if test failures detected)_

---

## 🔍 What would you like to investigate?

1. **Compare with master** - Check if these failures exist in recent master builds
2. **View specific logs** - Attempt to download logs for failed tasks (may fail due to API limits)
3. **Categorize failures** - Analyze failure types (infrastructure/flaky/real)
4. **Show full analysis** - Run complete analysis with all details

[View build in Azure DevOps](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>&view=logs)
```

### Phase 2: Detailed Output (Only If User Requests)

**If user requests comparison with master**:
```markdown
## Comparison with Master

**Baseline**: Build <MASTER_BUILD_ID> on `master` (finished <TIME_AGO>) - Status: ✅ Succeeded

**New Failures** (in PR but not in master):
- `<task_1>`
- `<task_2>`

**Pre-existing Failures** (also in master):
- `<task_3>`
- `<task_4>`

**Analysis**: <brief summary of findings>
```

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
- New failures not in master baseline
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
- Disk space: `No space left on device`, `ENOSPC`
- Container failures: `docker: Error response from daemon`

**Pattern examples**:
```
pull access denied, repository does not exist or may require authentication
TLS handshake timeout
Connection reset by peer
Build failed in XX:XX:XX (timeout)
```

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

**List Builds** (with query parameters):
```bash
az devops invoke --area build --resource builds \
  --route-parameters project=dd-trace-dotnet \
  --query-parameters branchName=refs/heads/master \$top=5
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

## Error Handling

### Build Not Found
- Check if PR has CI runs: `gh pr checks <PR> --repo DataDog/dd-trace-dotnet`
- Verify build ID is correct
- Check if build is still queued (not completed)

### Master Comparison Unavailable
- Show PR results only
- Warn that comparison could not be performed
- Suggest manual comparison using web UI

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
1. Compare with master
2. View specific logs
3. Categorize failures
4. Show full analysis
```

**User**: "Compare with master"

**Phase 2 Output** (after user request):
```
Comparing with master build 195277 (succeeded)...

All 8 failed tasks are NEW failures (not in master).
This indicates PR #7806 introduced breaking changes.
```

### Example 2: PR Analysis

**Command**: `/troubleshoot-ci-build pr 7806`

**Output**:
```
Found Azure DevOps build 195272 for PR #7806

[Quick summary as above]

What would you like to investigate?
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

### Understanding Test Results

1. **Build warnings vs test failures**:
   - `.issues` array contains warnings (build warnings, package warnings)
   - Check `.result == "failed"` for actual failures
   - Don't confuse build warnings with test failures

2. **Identifying the baseline**:
   - Most recent **succeeded** master build is best baseline
   - If master has recent failures, note them separately
   - Use build timestamps to ensure chronological order

3. **Log download failures**:
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
