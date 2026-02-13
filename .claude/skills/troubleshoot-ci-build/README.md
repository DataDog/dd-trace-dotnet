# CI Troubleshooting Skill

Automated CI failure analysis for the dd-trace-dotnet Azure DevOps pipeline.

## Purpose

This skill helps quickly identify, categorize, and prioritize CI failures by:
- Fetching build and test results from Azure DevOps
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

Automatically fetches the PR's CI build, identifies failures, and provides a summary.

### Analyze Specific Build

```bash
/troubleshoot-ci-build build <BUILD_ID>
```

**Example**:
```bash
/troubleshoot-ci-build build 195137
```

Useful when you have a build ID directly or want to analyze a specific build.

## Output

### Summary View (Default)

The skill provides a summary-first view:

```markdown
## CI Failure Summary for PR #7628 (Build 195137)

**Status**: ❌ Failed

### Quick Stats
- Failed jobs: 4
- Failed tests: 3 unique tests

### Failed Tests
| Test | Platforms | Category |
|------|-----------|----------|
| AzureFunctionsTests+IsolatedRuntimeV4.SubmitsTraces | Windows net6-10 | Real |
| AspNetCore5AsmInitializationSecurityEnabled.TestSecurityInitialization | Linux, Windows | Flaky? |

### Recommendations
1. **Investigate**: Azure Functions test (fails across multiple runtimes)
2. **Consider retry**: ASM test (known intermittent failure)
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
3. **Categorize**: Applies pattern matching to categorize failures
4. **Prioritize**: Highlights failures that need immediate attention

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
- Compilation errors
- Segmentation faults

## Requirements

- **PowerShell** - [Installation instructions](#installing-powershell)
  - **Recommended**: PowerShell 7+ (`pwsh`) - cross-platform, modern features
  - **Minimum**: PowerShell 5.1 (`powershell.exe` on Windows only)
- **GitHub CLI** (`gh`) authenticated (for PR analysis)
- **Azure CLI** (`az`) authenticated to DataDog organization
- **Internet connection** to fetch build data

### Installing PowerShell

**Recommended**: Install PowerShell 7+ for the best experience and cross-platform support.

**Windows**:
```powershell
winget install Microsoft.PowerShell
```

**macOS**:
```bash
brew install powershell/tap/powershell
```

**Linux (Ubuntu/Debian)**:
```bash
# Download the Microsoft repository GPG keys
wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb

# Register the Microsoft repository GPG keys
sudo dpkg -i packages-microsoft-prod.deb

# Update apt and install PowerShell
sudo apt-get update
sudo apt-get install -y powershell
```

**Linux (Other distributions)** and more details: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell

**Verify installation**:
```bash
pwsh -Version
# Should output: PowerShell 7.x.x or higher
```

## Related Scripts (tracer/tools/)

This skill uses the following standalone scripts that can also be run manually:

- **`Get-AzureDevOpsBuildAnalysis.ps1`** - Fetch and analyze Azure DevOps build failures, download logs

See [scripts-reference.md](./scripts-reference.md) for detailed documentation on using these scripts directly.

## Tips

- **Flaky Tests**: If a test fails on only one runtime but passes on others, it's likely flaky — retry first
- **Infrastructure**: Network/rate limiting issues usually resolve with a retry
- **Log Context**: When investigating, ask for detailed view to see log excerpts

## Examples

### Example 1: Quick PR Check

```bash
/troubleshoot-ci-build pr 7628
```

**Result**: Shows Azure Functions test failures that need investigation and ASM tests that are likely flaky (retry recommended).

### Example 2: Deep Dive on Failure

```bash
/troubleshoot-ci-build pr 7628
# After summary, ask:
"Show details for the Azure Functions failure"
```

**Result**: Full error message, log excerpt, related files, and recommended actions.

## Related Documentation

- [Troubleshooting CI Failures](../../../docs/development/CI/TroubleshootingCIFailures.md) - Manual troubleshooting guide
- [Run Smoke Tests Locally](../../../docs/development/CI/RunSmokeTestsLocally.md) - Reproducing CI failures locally
- [Common Failure Patterns](./failure-patterns.md) - Reference for categorization

## Limitations

- Only works for Azure DevOps builds (not AppVeyor or other CI systems)
- Large logs may be truncated (last 1000 lines)
- API rate limiting may require retry delays

## Future Enhancements

- Track failure trends over time
- Link to Datadog traces for failed tests
- Suggest specific files to check based on failure patterns
- Auto-create GitHub issues for new failures
