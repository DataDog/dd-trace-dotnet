# Troubleshooting CI Failures

This guide helps developers investigate build and test failures in the dd-trace-dotnet CI pipeline.

## CI Architecture Overview

The repository uses multiple CI systems:

- **Azure DevOps** - Primary build and test pipeline (Windows, Linux, macOS unit tests, integration tests)
- **GitHub Actions** - Verification tasks (CodeQL, nullability checks, source generators, snapshots)
- **GitLab** - Additional pipeline tasks

When investigating build failures, **check Azure DevOps first** as it runs the main build and test suite.

## Finding Failed Builds

### Using Azure CLI

#### 1. List recent builds for a PR

```bash
az pipelines runs list \
  --organization https://dev.azure.com/datadoghq \
  --project dd-trace-dotnet \
  --branch refs/pull/<PR_NUMBER>/merge \
  --top 30
```

#### 2. Filter for failed builds

```bash
az pipelines runs list \
  --organization https://dev.azure.com/datadoghq \
  --project dd-trace-dotnet \
  --branch refs/pull/<PR_NUMBER>/merge \
  --reason pullRequest \
  --query "[?result=='failed'].{id:id,finishTime:finishTime}" \
  --output table
```

#### 3. Get build details

```bash
az pipelines runs show \
  --organization https://dev.azure.com/datadoghq \
  --project dd-trace-dotnet \
  --id <BUILD_ID>
```

### Build URLs

The human-readable build URL format is:
```
https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>
```

### Using Azure DevOps MCP (AI Assistant Integration)

If you're using an AI assistant with the Azure DevOps MCP server, you can use these tools for cleaner queries:

#### Get build information
Ask your assistant to use `mcp__azure-devops__pipelines_get_builds` with:
- `project: "dd-trace-dotnet"`
- `buildIds: [<BUILD_ID>]`

This returns structured build data including status, result, queue time, and trigger information.

#### Get build logs
Ask your assistant to use `mcp__azure-devops__pipelines_get_build_log` with:
- `project: "dd-trace-dotnet"`
- `buildId: <BUILD_ID>`

Note: Large builds may have very large logs that exceed token limits. In that case, fall back to curl/jq to target specific log IDs.

#### Get specific log by ID
Ask your assistant to use `mcp__azure-devops__pipelines_get_build_log_by_id` with:
- `project: "dd-trace-dotnet"`
- `buildId: <BUILD_ID>`
- `logId: <LOG_ID>`
- Optional: `startLine` and `endLine` to limit output

**Advantages of MCP approach:**
- Structured JSON responses (no manual parsing)
- Works naturally in conversation with AI assistants
- Handles authentication automatically
- Can combine multiple queries in a single request

## Investigating Test Failures

### Find failed tasks in a build

```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/timeline" \
  | jq -r '.records[] | select(.result == "failed" or .result == "canceled") | "\(.name): \(.result) - \(.errorCount) errors"'
```

### Get log ID for a specific failed task

```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/timeline" \
  | jq -r '.records[] | select(.name == "Run unit tests" and .result == "failed") | .log.id' \
  | head -1
```

### Download and search logs

#### Option 1: Using curl (works everywhere)

```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>" \
  | grep -i "fail\|error"
```

#### Option 2: Using Azure CLI (recommended on Windows)

```bash
az rest --url "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>?api-version=7.0" \
  2>&1 | grep -i "fail\|error"
```

Note: You may see a warning about authentication - this is safe to ignore for public builds.

#### Option 3: Using GitHub CLI for quick overview

```bash
# Get quick summary of all checks for a PR
gh pr checks <PR_NUMBER>

# Get detailed PR status including links to Azure DevOps
gh pr view <PR_NUMBER> --json statusCheckRollup
```

### Get detailed context around failures

Using curl:

```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>" \
  | grep -A 30 "TestName.That.Failed"
```

Or with Azure CLI:

```bash
az rest --url "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>?api-version=7.0" \
  2>&1 | grep -A 30 "TestName.That.Failed"
```

## Mapping Commits to Builds

Azure DevOps builds test **merge commits** (`refs/pull/<PR_NUMBER>/merge`), not branch commits directly.

To find which branch commit caused a failure:

1. Get the build's queue time:
   ```bash
   az pipelines runs show \
     --organization https://dev.azure.com/datadoghq \
     --project dd-trace-dotnet \
     --id <BUILD_ID> \
     --query "{queueTime,startTime,finishTime}"
   ```

2. Compare with commit timestamps:
   ```bash
   git show --no-patch --format="%ci %h %s" <COMMIT_SHA>
   ```

The build queued shortly after the commit was pushed is likely testing that commit.

## Determining If Failures Are Related to Your Changes

When tests fail on master after your PR is merged, determine if failures are new or pre-existing:

### Compare with previous build on master

```bash
# List recent builds on master
az pipelines runs list \
  --organization https://dev.azure.com/datadoghq \
  --project dd-trace-dotnet \
  --branch master \
  --top 10 \
  --query "[].{id:id, result:result, sourceVersion:sourceVersion, finishTime:finishTime}" \
  --output table

# Find the build for the commit before yours
git log --oneline HEAD~1..HEAD  # Identify your commit and the previous one

# Compare failed tasks between builds
# Your build:
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<YOUR_BUILD_ID>/timeline" \
  | jq -r '.records[] | select(.result == "failed") | .name'

# Previous build:
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<PREVIOUS_BUILD_ID>/timeline" \
  | jq -r '.records[] | select(.result == "failed") | .name'
```

**New failures** only appear in your build → likely related to your changes.
**Same failures** appear in both → likely pre-existing/flaky tests.

### Master-only tests

Some tests (profiler integration tests, exploration tests) run only on master branch, not on PRs. If you see failures on master that you didn't see on your PR:

1. This is expected - those tests don't run on PRs
2. Compare with the previous successful master build to confirm they're new
3. The failures are likely related to your changes

## Understanding Test Infrastructure

When test failures don't make obvious sense, investigate the test infrastructure to understand how tests are configured.

### Finding test configuration

Tests may set up environments differently than production code. For example:

```bash
# Find how a specific test sets up environment variables
grep -r "DD_DOTNET_TRACER_HOME\|DD_TRACE_ENABLED" profiler/test/

# Look for test helper classes
find . -name "*EnvironmentHelper*.cs" -o -name "*TestRunner*.cs"

# Check what environment variables a test actually sets
# Read the test code path from failing test name:
# Example: Datadog.Profiler.SmokeTests.WebsiteAspNetCore01Test.CheckSmoke
# Path: profiler/test/Datadog.Profiler.IntegrationTests/SmokeTests/WebsiteAspNetCore01Test.cs
```

**Common gotchas:**
- Profiler tests may disable the tracer (`DD_TRACE_ENABLED=0`)
- Different test suites (tracer vs profiler) have different configurations
- Test environment may not match production deployment

### Cross-cutting test failures

Changes in one component may affect tests for another component:

- **Managed tracer changes** may affect profiler tests (they share the managed loader)
- **Native changes** may affect managed tests (if they change initialization order)
- **Environment variable handling** may affect both tracer and profiler

**Investigation strategy:**
1. Identify which component the failing test is for (tracer, profiler, debugger, etc.)
2. Compare with your changes - do they touch shared infrastructure?
3. Check if test configuration differs from production (e.g., disabled features)
4. Trace through initialization code to find the interaction point

### Tracing error messages to source code

When you find an error message in logs, trace it back to source code:

```bash
# Search for the error message across the codebase
grep -r "One or multiple services failed to start" .

# Example output:
# profiler/src/ProfilerEngine/Datadog.Profiler.Native/CorProfilerCallback.cpp:710
#     Log::Error("One or multiple services failed to start after a delay...");
```

This helps you understand:
- Which component is logging the error (native/managed, tracer/profiler)
- The context of the failure (initialization, shutdown, runtime)
- Related code that might be affected

## Common Test Failure Patterns

### Infrastructure Failures (Not Your Code)

Some failures are infrastructure-related and can be retried without code changes:

#### Docker Rate Limiting

```
toomanyrequests: You have reached your unauthenticated pull rate limit. https://www.docker.com/increase-rate-limit
```

**Solution**: Retry the failed job in Azure DevOps. This is a transient Docker Hub rate limit issue.

#### Timeout/Network Issues

```
##[error]The job running on runner X has exceeded the maximum execution time
TLS handshake timeout
Connection reset by peer
```

**Solution**: Retry the failed job. These are typically transient network issues.

#### Identifying Flaky Tests and Retry Attempts

Azure DevOps automatically retries some failed stages. You can identify retried tasks in the build timeline:

**Using curl/jq:**
```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/timeline" \
  | jq -r '.records[] | select(.previousAttempts != null and (.previousAttempts | length) > 0) | "\(.name): attempt \(.attempt), previous attempts: \(.previousAttempts | length)"'
```

**Using Azure DevOps MCP:**
Ask your assistant to check the build timeline for tasks with `previousAttempts` or `attempt > 1`.

**What this means:**
- `"attempt": 2` with `"result": "succeeded"` → The task failed initially but passed on retry (likely a flake)
- `"previousAttempts": [...]` → Contains IDs of previous failed attempts

**When you see retried tasks:**
1. If a task succeeded on retry after an initial failure, it's likely a flaky/intermittent issue
2. The overall build result may still show as "failed" even if the retry succeeded, depending on pipeline configuration
3. Check if the failure pattern is known (see "Flaky Profiler Stack Walking Failures" below)

**How to retry a failed job:**
1. Open the build in Azure DevOps: `https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=<BUILD_ID>`
2. Find the failed stage/job
3. Click the "..." menu → "Retry failed stages" or "Retry stage"
4. Only failed stages will be retried; successful stages are not re-run

### Unit Test Failures

Failed unit tests typically appear in logs as:
```
[xUnit.net 00:00:02.94]     Namespace.Class.TestMethod [FAIL]
```

Look for:
- `Error Message:` - The assertion failure message
- `Stack Trace:` - Where the test failed
- `Expected` vs `Actual` - What the test was checking

### Integration Test Failures

Integration test failures may indicate:
- Docker container issues
- Service dependency problems
- Network/timing issues
- Snapshot mismatches

Check the specific integration test logs for details about which service or scenario failed.

#### Flaky Profiler Stack Walking Failures (Alpine/musl)

**Symptom:**
```
Failed to walk N stacks for sampled exception: E_FAIL (80004005)
```
or
```
Failed to walk N stacks for sampled exception: CORPROF_E_STACKSNAPSHOT_UNSAFE
```

**Appears in**: Smoke tests on Alpine Linux (musl libc), particularly `installer_smoke_tests` → `linux alpine_3_1-alpine3_14`

**Cause**: Race condition in the profiler when unwinding call stacks while threads are running. This is a known limitation on Alpine/musl platforms and appears intermittently.

**Solution**: Retry the failed job. The smoke test check `CheckSmokeTestsForErrors` has an allowlist for known patterns, but some error codes like `E_FAIL` may occasionally slip through.

**Note**: The profiler only logs these warnings every 100 failures to avoid log spam, so seeing this message indicates multiple stack walking attempts have failed.

### Build Failures

Build failures typically show:
```
##[error]Target "BuildTracerHome" has thrown an exception
```

Check for:
- Compilation errors
- Missing dependencies
- Configuration issues

## GitHub Actions Checks

GitHub Actions run verification tasks that can also fail:

```bash
gh run list --branch <BRANCH_NAME> --limit 20
```

Common verification failures:
- **verify_source_generators** - Source-generated files need updating (run `./tracer/build.sh` locally)
- **verify_files_without_nullability** - Nullability annotations out of sync
- **verify_app_trimming_descriptor_generator** - Trimming descriptors need updating
- **Check snapshots** - Test snapshots don't match (see [RunSmokeTestsLocally.md](RunSmokeTestsLocally.md))

## Example Investigation Workflow

### Quick Investigation (AI Assistant with MCP)

If you're using an AI assistant with Azure DevOps MCP:

```
"Why did Azure DevOps build <BUILD_ID> fail?"
```

The assistant will:
1. Get build information using `mcp__azure-devops__pipelines_get_builds`
2. Identify the result and any failed stages
3. Check for retry attempts to identify flaky tests
4. Provide guidance on whether to retry or investigate further

### Quick Investigation (GitHub CLI)

```bash
# 1. Get quick overview of all checks
gh pr checks <PR_NUMBER>

# 2. If Azure DevOps checks failed, check the logs directly
# Get the build ID from the Azure DevOps URL in the output above, then:
BUILD_ID=<build_id_from_checks>
az rest --url "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/${BUILD_ID}/logs/<LOG_ID>?api-version=7.0" \
  2>&1 | grep -i "error\|fail\|toomanyrequests"
```

### Detailed Investigation

```bash
# 1. Find your PR number
gh pr list --head <BRANCH_NAME>

# 2. List failed builds for the PR
az pipelines runs list \
  --organization https://dev.azure.com/datadoghq \
  --project dd-trace-dotnet \
  --branch refs/pull/<PR_NUMBER>/merge \
  --query "[?result=='failed'].{id:id,finishTime:finishTime}"

# 3. Find failed tasks
BUILD_ID=<your_build_id>
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/${BUILD_ID}/timeline" \
  | jq -r '.records[] | select(.result == "failed") | "\(.name): log.id=\(.log.id)"'

# 4. Download and examine logs (choose one method)
LOG_ID=<log_id_from_above>

# Using curl:
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/${BUILD_ID}/logs/${LOG_ID}" \
  | grep -A 30 "FAIL"

# Or using Azure CLI (Windows):
az rest --url "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/${BUILD_ID}/logs/${LOG_ID}?api-version=7.0" \
  2>&1 | grep -A 30 "FAIL"

# 5. Open build in browser for full details
open "https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=${BUILD_ID}"
```
