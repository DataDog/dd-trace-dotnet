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

```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>" \
  | grep -i "fail\|error"
```

### Get detailed context around failures

```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>" \
  | grep -A 30 "TestName.That.Failed"
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

## Common Test Failure Patterns

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

# 4. Download and examine logs
LOG_ID=<log_id_from_above>
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds/${BUILD_ID}/logs/${LOG_ID}" \
  | grep -A 30 "FAIL"

# 5. Open build in browser for full details
open "https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=${BUILD_ID}"
```
