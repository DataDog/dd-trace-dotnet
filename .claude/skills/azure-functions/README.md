# Azure Functions Skill

A Claude Code skill for tracer engineers to build, deploy, and test dev versions of the `Datadog.AzureFunctions` NuGet package against Azure Function Apps.

## Usage

Invoke the skill with `/azure-functions` followed by an optional command:

```
/azure-functions                          # Interactive mode - guided workflow
/azure-functions build-nuget              # Build Datadog.AzureFunctions NuGet package
/azure-functions deploy [app-name]        # Deploy to Azure Function App
/azure-functions test [app-name]          # Trigger and verify function
/azure-functions logs [app-name]          # Download and analyze logs
/azure-functions trace [trace-id]         # Analyze specific trace in Datadog
/azure-functions configure [app-name]     # Configure environment variables
```

## Features

### Build
- Builds `Datadog.AzureFunctions` NuGet package with your changes
- Generates unique prerelease version (avoids NuGet cache issues)
- Publishes to bundle folder
- Copies to user-specified output directory

### Deploy
- Deploys sample app to Azure Function App
- User provides app name and resource group
- Reminds to wait for worker restart

### Test
- Triggers HTTP endpoint
- Captures execution timestamp (for log filtering)
- Verifies HTTP 200 response

### Logs
- Downloads logs from Azure
- Extracts to a timestamped directory
- Provides timestamp-based filtering commands
- Identifies host vs worker processes
- Verifies tracer version
- Analyzes trace context flow

### Trace
- Queries Datadog API for specific trace
- Shows span hierarchy
- Verifies parent-child relationships
- Checks process tags (`host` vs `worker`)

## Files

- **SKILL.md** - Main skill definition with workflow steps
- **log-analysis-guide.md** - Detailed log analysis patterns and grep examples
- **scripts-reference.md** - Reusable bash/PowerShell scripts for common tasks
- **environment-variables.md** - Complete reference for Azure Functions environment variable configuration
- **README.md** - This file

## Skill Scripts

Utility scripts included with this skill:

- **Find-NuGetConfig.ps1** - Search for `nuget.config` file by walking up directory hierarchy. Used to verify sample apps have access to local NuGet feed before deployment.

## Related Scripts (tracer/tools/)

Standalone PowerShell scripts for Azure Functions workflows:

- **Build-AzureFunctionsNuget.ps1** - Build Datadog.AzureFunctions NuGet package
- **Deploy-AzureFunction.ps1** - Automate deployment, wait, trigger, and timestamp capture
- **Get-AzureFunctionLogs.ps1** - Download, extract, and analyze logs with tracer version, span count, and parenting checks
- **Get-DatadogTrace.ps1** - Retrieve all spans for a trace ID from the Datadog API (table, JSON, or hierarchy output)
- **Get-DatadogLogs.ps1** - Query logs from the Datadog Logs API (table, JSON, or raw output)

See [scripts-reference.md](scripts-reference.md) for detailed usage examples and pipeline patterns.

## Interactive Mode

When invoked without arguments (`/azure-functions`), the skill guides you through:

1. Understanding your goal (new feature, bug fix, trace verification)
2. Building the NuGet package
3. Selecting target app
4. Deploying to Azure
5. Waiting for worker restart
6. Testing function execution
7. Downloading logs
8. Analyzing results based on your goal
9. Running verification checklist

## Quick Reference

### Prerequisites

**PowerShell**:
- **Recommended**: PowerShell 7+ (`pwsh`) - cross-platform, modern features
- **Minimum**: PowerShell 5.1 (`powershell.exe` on Windows only)
- [Installation instructions](#installing-powershell)

**Azure resources** (users provide their own):
- **App name** (`-AppName`): The Azure Function App to deploy to
- **Resource group** (`-ResourceGroup`): The Azure resource group containing the app
- **Sample app path** (`-SampleAppPath`): Local path to an Azure Functions app that references the `Datadog.AzureFunctions` NuGet package

The sample app must have a `nuget.config` (in the app directory or a parent directory) that defines a local NuGet feed. The `-CopyTo` parameter of `Build-AzureFunctionsNuget.ps1` should point to the same directory as that local feed so `dotnet restore` picks up the freshly built package.

### Installing PowerShell

PowerShell 5.1+ is required. Windows 10/11 includes PowerShell 5.1 (`powershell.exe`). For PowerShell 7+ (`pwsh`, recommended):

- **Windows**: `winget install Microsoft.PowerShell`
- **macOS**: `brew install powershell/tap/powershell`
- **Linux**: See [Microsoft docs](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell)

## Documentation References

- `docs/development/AzureFunctions.md` - Complete Azure Functions integration guide
- `docs/development/for-ai/AzureFunctions-Architecture.md` - Architecture deep dive

## Examples

### Build and deploy to primary app
```
/azure-functions build-nuget
/azure-functions deploy
```

### Test specific app and download logs
```
/azure-functions test my-function-app
/azure-functions logs my-function-app
```

### Analyze specific trace
```
/azure-functions trace 68e948220000000047fef7bad8bb854e
```

### Full workflow (interactive)
```
/azure-functions
```

## Verification Checklist

After deployment and testing, the skill helps verify:

- [ ] Function responds successfully (HTTP 200)
- [ ] Worker loaded correct tracer version
- [ ] Host and worker spans share same trace ID
- [ ] Span parent-child relationships are correct
- [ ] Process tags are correct (`aas.function.process:host` or `worker`)
- [ ] No error logs at execution timestamp
- [ ] AsyncLocal context flows correctly (if debugging context issues)

## Common Troubleshooting

The skill provides guidance for:

- Function not responding after deployment
- Wrong tracer version after deployment
- Traces not appearing in Datadog
- Separate traces (span parenting issues)
- Missing debug logs
- AsyncLocal context flow issues
- Environment variable configuration (see [environment-variables.md](environment-variables.md))

## Tips

- Always use timestamp filtering when analyzing logs
- Verify tracer version before investigating behavior
- Follow trace IDs from host to worker
- Check span parent-child relationships
- Download logs after each test execution
- Enable debug logging for detailed investigation
