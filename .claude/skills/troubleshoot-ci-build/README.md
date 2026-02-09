# CI Troubleshooting Skill

Automated CI failure analysis for the dd-trace-dotnet Azure DevOps pipeline.

## Purpose

This skill helps quickly identify, categorize, and prioritize CI failures by:
- Fetching build and test results from Azure DevOps
- Comparing against recent master builds to identify new vs pre-existing failures
- Categorizing failures (infrastructure, flaky, real)
- Providing actionable recommendations

## Usage

### Analyze PR Failures

```bash
/troubleshoot-ci-build pr <PR_NUMBER>
```

**Example**:
```bash
/troubleshoot-ci-build pr 7628
```

Automatically fetches the PR's CI build, identifies failures, compares with master, and provides a summary.

### Analyze Specific Build

```bash
/troubleshoot-ci-build build <BUILD_ID>
```

**Example**:
```bash
/troubleshoot-ci-build build 195137
```

Useful when you have a build ID directly or want to analyze a master/branch build.

### Manual Build Comparison

```bash
/troubleshoot-ci-build compare <BUILD_ID> <BASELINE_BUILD_ID>
```

**Example**:
```bash
/troubleshoot-ci-build compare 195137 195120
```

Compare two specific builds to see what changed.

## Output

### Summary View (Default)

The skill provides a summary-first view:

```markdown
## CI Failure Summary for PR #7628 (Build 195137)

**Status**: ❌ Failed

### Quick Stats
- Failed jobs: 4
- Failed tests: 3 unique tests
- New failures: 1 (not in master)

### Failed Tests
| Test | Platforms | New? | Category |
|------|-----------|------|----------|
| AzureFunctionsTests+IsolatedRuntimeV4.SubmitsTraces | Windows net6-10 | ✅ New | Real |
| AspNetCore5AsmInitializationSecurityEnabled.TestSecurityInitialization | Linux, Windows | ❌ In master | Flaky? |

### Recommendations
1. **Investigate**: Azure Functions test (new failure)
2. **Consider retry**: ASM test (also in master)
```

### Detailed View (On Request)

Ask for details on a specific failure to get:
- Full error messages and stack traces
- Log excerpts around the failure
- Recent history (when did it start failing?)
- Similar failures in the same build
- Related files from the PR diff

## How It Works

1. **Fetch Build Info**: Uses GitHub CLI and Azure DevOps API to get build details
2. **Parse Logs**: Downloads task logs and extracts test failures
3. **Compare with Master**: Automatically fetches recent master build for comparison
4. **Categorize**: Applies pattern matching to categorize failures
5. **Prioritize**: Highlights new failures that need immediate attention

## Failure Categories

### Infrastructure (Recommend: Retry)
- Docker rate limiting
- Network timeouts
- Disk space issues

### Flaky (Recommend: Retry, May Investigate)
- Tests that already retried (have `previousAttempts`)
- Known intermittent issues
- Stack walking failures on Alpine

### Real (Recommend: Investigate)
- Test assertions
- New failures not in master
- Compilation errors
- Segmentation faults

## Requirements

- **GitHub CLI** (`gh`) authenticated (for PR analysis)
- **Azure CLI** (`az`) authenticated to DataDog organization
- **Internet connection** to fetch build data

## Related Scripts (tracer/tools/)

This skill uses the following standalone scripts that can also be run manually:

- **`Get-AzureDevOpsBuildAnalysis.ps1`** - Fetch and analyze Azure DevOps build failures, compare with baselines, download logs

See [scripts-reference.md](./scripts-reference.md) for detailed documentation on using these scripts directly.

## Tips

- **New vs Pre-existing**: Focus on "New" failures first - these are likely caused by your PR changes
- **Flaky Tests**: If a test is also failing in master, consider retrying before investigating
- **Infrastructure**: Network/rate limiting issues usually resolve with a retry
- **Log Context**: When investigating, ask for detailed view to see log excerpts

## Examples

### Example 1: Quick PR Check

```bash
/troubleshoot-ci-build pr 7628
```

**Result**: Shows 1 new Azure Functions test failure that needs investigation, 2 ASM tests also failing in master (retry recommended).

### Example 2: Deep Dive on Failure

```bash
/troubleshoot-ci-build pr 7628
# After summary, ask:
"Show details for the Azure Functions failure"
```

**Result**: Full error message, log excerpt, related files, and recommended actions.

### Example 3: Compare Builds

```bash
/troubleshoot-ci-build compare 195137 195120
```

**Result**: Shows what tests changed between builds (new failures, new passes, still failing).

## Related Documentation

- [Troubleshooting CI Failures](../../../docs/development/CI/TroubleshootingCIFailures.md) - Manual troubleshooting guide
- [Run Smoke Tests Locally](../../../docs/development/CI/RunSmokeTestsLocally.md) - Reproducing CI failures locally
- [Common Failure Patterns](./failure-patterns.md) - Reference for categorization

## Limitations

- Only works for Azure DevOps builds (not AppVeyor or other CI systems)
- Comparison with master requires recent master build (within last 5 builds)
- Large logs may be truncated (last 1000 lines)
- API rate limiting may require retry delays

## Future Enhancements

- Track failure trends over time
- Link to Datadog traces for failed tests
- Suggest specific files to check based on failure patterns
- Auto-create GitHub issues for new failures
