---
name: azure-functions
description: Dev/test workflow for tracer engineers working on the Datadog .NET tracer — build a local Datadog.AzureFunctions NuGet package, deploy it to a test Azure Function App, trigger it, and analyze traces/logs to verify instrumentation behavior. Use this skill whenever the user is working on Azure Functions instrumentation: building or testing the Datadog.AzureFunctions NuGet package, deploying to a test Function App, analyzing instrumentation logs or spans from an Azure Functions app, or configuring Datadog environment variables on Azure — even if they don't explicitly invoke /azure-functions.
argument-hint: [build-nuget|deploy|test|logs|configure]
allowed-tools: Bash(pwsh *) Bash(az functionapp show *) Bash(az functionapp list *) Bash(az functionapp list-functions *) Bash(az functionapp function list *) Bash(az functionapp function show *) Bash(az functionapp config appsettings list *) Bash(az functionapp config appsettings set *) Bash(az functionapp config appsettings delete *) Bash(az functionapp config show *) Bash(az functionapp deployment list *) Bash(az functionapp deployment show *) Bash(az functionapp deployment source show *) Bash(az functionapp plan list *) Bash(az functionapp plan show *) Bash(az functionapp restart *) Bash(az functionapp stop *) Bash(az functionapp start *) Bash(az webapp log download *) Bash(az webapp log tail *) Bash(az group list *) Bash(az group show *) Bash(curl *) Bash(func azure functionapp publish *) Bash(func azure functionapp logstream *) Bash(func azure functionapp list-functions *) Bash(func azure functionapp fetch-app-settings *) Bash(func azure functionapp fetch *) Bash(dotnet restore) Bash(dotnet clean) Bash(dotnet build *) Bash(unzip *) Read
---

# Azure Functions Dev/Test Workflow

This skill helps tracer engineers test changes to the `Datadog.AzureFunctions` package: build a local dev version, deploy it to a test Azure Function App, trigger the function, and analyze traces/logs to verify instrumentation behavior.

## Prerequisites

This skill requires the following tools (assume they are installed and only troubleshoot if errors occur):

- **PowerShell**: `pwsh` (PowerShell 7+) preferred, or `powershell.exe` (PowerShell 5.1+ on Windows)
  - Always prefer `pwsh` over `powershell.exe` when available
  - Scripts use PowerShell-specific cmdlets like `Expand-Archive`
- **Azure CLI**: `az` (must be authenticated)
- **Azure Functions Core Tools**: `func`
- **.NET SDK**: Matching target framework of sample app

**Only if a tool fails, provide installation links**:
- **PowerShell**: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell
- **Azure CLI**: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
- **Azure Functions Core Tools**: https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local
- **.NET SDK**: https://dotnet.microsoft.com/download

## Commands

When invoked with an argument, perform the corresponding workflow:

- `/azure-functions build-nuget` - Build the Datadog.AzureFunctions NuGet package
- `/azure-functions deploy [app-name]` - Deploy to Azure Function App
- `/azure-functions configure [app-name]` - Configure environment variables for Datadog instrumentation
- `/azure-functions test [app-name]` - Trigger and verify function execution
- `/azure-functions logs [app-name]` - Download and analyze logs

If no argument is provided, guide the user through the full workflow interactively.

## Context

**Current repository**: This skill assumes you are working from the root of the `dd-trace-dotnet` repository.

**Prerequisites**: Users provide their own Azure Function App name (`-AppName`), resource group (`-ResourceGroup`), and sample app path (`-SampleAppPath`). The sample app must:
1. Reference the `Datadog.AzureFunctions` NuGet package
2. Have a `nuget.config` file (in the app directory or a parent directory) that defines a local NuGet feed pointing to a directory on disk, for example:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <clear />
       <add key="local" value="nuget/local-source" />
       <add key="nuget" value="https://api.nuget.org/v3/index.json" />
     </packageSources>
   </configuration>
   ```
   The `-CopyTo` parameter of `Build-AzureFunctionsNuget.ps1` must point to the same directory as the local feed (e.g. if the feed `value` is `nuget/local-source` relative to the `nuget.config` location, then `-CopyTo` should be the absolute path to that directory).

## Workflow Steps

### 1. Build NuGet Package

**CRITICAL**: Before building, verify that `tracer/src/Datadog.AzureFunctions/Datadog.AzureFunctions.csproj` uses **PackageReference** (not ProjectReference) for `Datadog.Trace` and `Datadog.Trace.Annotations`. This ensures the locally-built package references the latest releases from nuget.org instead of building them from source.

**Check the .csproj** — it should contain:
```xml
<PackageReference Include="Datadog.Trace" Version="*"/>
<PackageReference Include="Datadog.Trace.Annotations" Version="*" />
```

If instead it contains **ProjectReference** lines like these, replace them with the PackageReference lines above:
```xml
<!-- These are the production references — replace for local testing: -->
<ProjectReference Include="$(MSBuildThisFileDirectory)..\Datadog.Trace.Manual\Datadog.Trace.Manual.csproj" />
<ProjectReference Include="$(MSBuildThisFileDirectory)..\Datadog.Trace.Annotations\Datadog.Trace.Annotations.csproj" />
```

**IMPORTANT**: The PackageReference change is for local testing only. Do NOT commit it. If it's already using PackageReference, no change is needed.

Build the `Datadog.AzureFunctions` NuGet package with your changes:

```powershell
./tracer/tools/Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir>
```

**What this does**:
1. (If `-BuildId` specified and files not already downloaded) Downloads bundle from Azure DevOps build once
2. Generates a unique prerelease version from a timestamp (e.g. `3.38.0-dev20260209143022`)
3. Cleans previous builds
4. Builds `Datadog.Trace` (net6.0)
5. Publishes to bundle folder
6. Packages `Datadog.AzureFunctions.nupkg` with the generated version (referencing latest nuget.org releases)
7. Copies to the directory specified by `-CopyTo`

**Versioning**: Each build gets a unique version, so NuGet caching is never an issue.
The sample app should use a floating version like `3.38.0-dev.*` in its package reference
(or `Directory.Packages.props`) to always resolve the latest local dev build.

**Options**:
- `-CopyTo <output-dir>` - Copy the built package to the specified directory (typically your local NuGet feed)
- `-Version '3.38.0-dev.custom'` - Use a specific version instead of auto-generating
- `-BuildId 12345` - One-time download of bundle files from Azure DevOps build (only needed once per dd-trace-dotnet release, then reused for subsequent local builds)

**Examples**:
```powershell
# Typical local build (after bundle files already downloaded)
./tracer/tools/Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir>

# First build after new dd-trace-dotnet release (download bundle files once)
./tracer/tools/Build-AzureFunctionsNuget.ps1 -BuildId 12345 -CopyTo <output-dir>
```

### 2. Deploy Function

**IMPORTANT**: Before deploying, verify prerequisites:

**Verify nuget.config exists**:
```powershell
$nugetConfig = ./.claude/skills/azure-functions/Find-NuGetConfig.ps1 -StartPath "<path-to-sample-app>"
if (-not $nugetConfig) {
    Write-Error "nuget.config not found in sample app directory or parent directories"
    exit 1
}
Write-Host "Found nuget.config at: $nugetConfig"
```

**Verify environment variables are configured** (skip if already done on a previous deploy):
```powershell
$envCheck = ./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "<app-name>" -ResourceGroup "<resource-group>"
if (-not $envCheck.AllRequiredPresent) {
    Write-Warning "Required environment variables are missing. Run '/azure-functions configure' first, or proceed if you plan to configure after deploying."
}
```

Use the `Deploy-AzureFunction.ps1` script to automate deployment, wait, and trigger:

```powershell
./tracer/tools/Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"
```

**What this does**:
1. Runs `dotnet restore` in the sample app directory
2. Publishes to Azure with `func azure functionapp publish`
3. Waits 60 seconds (default) for worker process to restart
4. Triggers the HTTP endpoint and captures execution timestamp
5. Outputs a result object for pipeline usage

**Options**:
- `-SkipBuild` - Skip `dotnet restore`
- `-SkipWait` - Skip 2-minute wait (not recommended)
- `-WaitSeconds 60` - Custom wait duration
- `-SkipTrigger` - Skip HTTP trigger
- `-TriggerUrl "https://..."` - Custom trigger URL

**Note**: `-AppName` and `-ResourceGroup` are required parameters.

**Pipeline usage** (save output for log analysis):
```powershell
$deploy = ./tracer/tools/Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"
```

### 3. Configure Environment Variables

Configure Datadog instrumentation environment variables for an Azure Function App using `Set-EnvVars.ps1`:

```powershell
./.claude/skills/azure-functions/Set-EnvVars.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -ApiKey "<api-key>" `
  -Tier recommended `
  -Env "dev-lucas"
```

**What this does**:
1. Detects the OS/platform (Linux or Windows)
2. Builds the correct set of variables for the chosen tier
3. Sets platform-specific profiler paths automatically
4. Applies all settings via Azure CLI
5. Restarts the function app (unless `-SkipRestart`)

**Tiers**:
- **required** - Minimum variables for instrumentation (CORECLR_*, DD_API_KEY, DD_DOTNET_TRACER_HOME, DOTNET_STARTUP_HOOKS)
- **recommended** - required + feature disables (AppSec, CI Visibility, RCM, Agent Feature Polling, Process)
- **debug** - recommended + debug logging (DD_TRACE_DEBUG, DD_LOG_LEVEL, DD_TRACE_LOG_SINKS, direct log submission)

**Options**:
- `-Tier required|recommended|debug` - Configuration tier (default: required)
- `-Env "dev-lucas"` - Set DD_ENV
- `-Service "my-service"` - Set DD_SERVICE
- `-Version "1.0.0"` - Set DD_VERSION
- `-SamplingRules '<json>'` - Set DD_TRACE_SAMPLING_RULES
- `-ExtraSettings @{"KEY"="value"}` - Set additional variables
- `-SkipRestart` - Don't restart the app after applying
- `-WhatIf` - Preview changes without applying

**Preview before applying**:
```powershell
./.claude/skills/azure-functions/Set-EnvVars.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -ApiKey "<api-key>" `
  -Tier debug `
  -WhatIf
```

**Complete reference**: See [environment-variables.md](environment-variables.md) for all available variables.

### 4. Test Function

Trigger an already-deployed function and capture the execution timestamp. Useful for re-testing after a deploy, or testing an app that was deployed earlier.

**Discover available triggers**:
```bash
# List HTTP-triggered functions and their URLs
func azure functionapp list-functions <app-name> --show-keys
```

Or via Azure CLI:
```bash
az functionapp function list --name <app-name> --resource-group <resource-group> --query "[].{name:name, href:invokeUrlTemplate}" -o table
```

**Trigger and capture timestamp**:
```powershell
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
$response = Invoke-WebRequest -Uri "<trigger-url>" -UseBasicParsing
Write-Host "HTTP Status: $($response.StatusCode)"
Write-Host "Execution timestamp (UTC): $timestamp"
```

Save the timestamp — you'll need it for log filtering in the next step.

**Note**: The Deploy script (step 2) already triggers and captures a timestamp. Use this step when you want to re-test without redeploying.

### 5. Download and Analyze Logs

Use the `Get-AzureFunctionLogs.ps1` script to download, extract, and analyze logs:

```powershell
./tracer/tools/Get-AzureFunctionLogs.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -OutputPath $env:TEMP `
  -ExecutionTimestamp "<YYYY-MM-DD HH:MM:SS>" `
  -All
```

**IMPORTANT**: Always specify `-OutputPath $env:TEMP` to save logs to the system temp folder instead of cluttering the repository directory.

**What this does**:
1. Downloads logs from Azure to a timestamped zip file
2. Extracts the archive
3. Identifies host and worker log files
4. Analyzes tracer version, span count, and trace parenting

**Analysis options**:
- `-ShowVersion` - Display Datadog tracer version from worker logs
- `-ShowSpans` - Count spans at execution timestamp (split by host/worker)
- `-CheckParenting` - Validate trace parenting (detect root span duplication)
- `-All` - Enable all analysis (recommended)

**Pipeline usage** (with Deploy script):
```powershell
$deploy = ./tracer/tools/Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"

./tracer/tools/Get-AzureFunctionLogs.ps1 `
  -AppName $deploy.AppName `
  -ResourceGroup "<resource-group>" `
  -OutputPath $env:TEMP `
  -ExecutionTimestamp $deploy.ExecutionTimestamp `
  -All
```

**Log file patterns**:
- **Host process**: `dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-{pid}.log`
- **Worker process**: `dotnet-tracer-managed-dotnet-{pid}.log`

**CRITICAL**: The script automatically filters logs by execution timestamp. Raw log files are append-only and contain entries from multiple deployments/restarts.

**Parenting analysis**:
- **Span fields**: `s_id` (span ID), `p_id` (parent ID), `t_id` (trace ID)
- **Healthy trace**: Worker spans have same `t_id` as host, `p_id` matching host span IDs
- **Broken trace**: Worker spans have different `t_id` or `p_id: null` (orphaned root)

## Verification Checklist

After deployment and testing:

- [ ] Function responds successfully (HTTP 200)
- [ ] Worker loaded correct tracer version (check "Assembly metadata")
- [ ] Host and worker spans share same trace ID
- [ ] Span parent-child relationships are correct (check `p_id` → `s_id` links)
- [ ] Process tags are correct (`aas.function.process:host` or `worker`)
- [ ] No error logs at execution timestamp
- [ ] AsyncLocal context flows correctly (if debugging context issues)

## Common Troubleshooting

**General guidance**: For environment variable configuration issues, see [environment-variables.md](environment-variables.md) for complete reference on required, recommended, and debugging variables.

### Function Not Responding

**First, check if the app is running**:
```powershell
$envCheck = ./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "<app-name>" -ResourceGroup "<resource-group>"
if ($envCheck.State -ne "Running") {
    Write-Host "App is '$($envCheck.State)' — starting it..."
    az functionapp start --name <app-name> --resource-group <resource-group>
}
```

If the app is running but not responding:
```bash
# Restart function app
az functionapp restart --name <app-name> --resource-group <resource-group>
```

### Deployment Fails (`func azure functionapp publish`)

If the publish command fails, diagnose with:
```bash
# Check recent deployment status
az functionapp deployment list \
  --name <app-name> \
  --resource-group <resource-group> \
  --query "[0].{status:status, message:message, startTime:startTime}" -o table

# Stream live logs to see startup errors
func azure functionapp logstream <app-name>
```

Common causes:
- **Auth expired**: Run `az login` and retry
- **App not running**: Start it first with `az functionapp start --name <app-name> --resource-group <resource-group>`
- **Build errors**: Check `dotnet restore` output in the sample app directory

### Wrong Tracer Version After Deployment
```bash
# Check all worker initializations
grep "Assembly metadata" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log

# If old version, rebuild from the dd-trace-dotnet repo root (each build gets a unique version, no cache issues)
./tracer/tools/Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose
# Then restore and redeploy the sample app
```

### Traces Not Appearing in Datadog

**Verify all required environment variables** (including DD_API_KEY, profiler paths, etc.):
```powershell
./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "<app-name>" -ResourceGroup "<resource-group>" -IncludeRecommended
```

If all env vars pass, check worker initialization in logs:
```bash
grep "Datadog Tracer initialized" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

**Complete reference**: See [environment-variables.md](environment-variables.md) for all available variables.

### Separate Traces (Parenting Issue)
1. Get trace ID from host logs at execution timestamp
2. Search worker logs for same trace ID
3. If not found → worker created separate trace
4. Look for worker spans with `p_id: null` instead of parent IDs matching host spans
5. Enable debug logging: `az functionapp config appsettings set --name <app> --resource-group <resource-group> --settings DD_TRACE_DEBUG=1`
6. Re-test and analyze debug messages about AsyncLocal context flow

## Query Datadog API

When traces or logs have reached Datadog (e.g., verifying spans look correct, correlating logs with a trace ID), use these scripts. Both require `DD_API_KEY` and `DD_APPLICATION_KEY` environment variables.

**Retrieve all spans for a trace ID**:
```powershell
# Table view (default)
./tracer/tools/Get-DatadogTrace.ps1 -TraceId "<trace-id>"

# Hierarchy view — shows span parent-child tree with process tags
./tracer/tools/Get-DatadogTrace.ps1 -TraceId "<trace-id>" -OutputFormat hierarchy

# Search further back in time (default: 2h)
./tracer/tools/Get-DatadogTrace.ps1 -TraceId "<trace-id>" -TimeRange "1d"
```

**Query logs from Datadog**:
```powershell
./tracer/tools/Get-DatadogLogs.ps1 -Query "service:<app-name>"
./tracer/tools/Get-DatadogLogs.ps1 -Query "service:<app-name> error" -TimeRange "2h" -Limit 100
```

See [scripts-reference.md](scripts-reference.md) for full parameter reference.

## Additional Resources

- **Log analysis**: [log-analysis-guide.md](log-analysis-guide.md) - Manual log investigation patterns (when the automated `-All` analysis isn't sufficient)
- **Scripts**: [scripts-reference.md](scripts-reference.md) - Reusable PowerShell scripts and one-liners
- **Environment variables**: [environment-variables.md](environment-variables.md) - Complete reference for Azure Functions configuration

## References

- Detailed docs: `docs/development/AzureFunctions.md`
- Architecture deep dive: `docs/development/for-ai/AzureFunctions-Architecture.md`
