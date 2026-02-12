---
name: azure-functions
description: Dev/test workflow for tracer engineers — build a local Datadog.AzureFunctions NuGet package, deploy to a test Azure Function App, trigger it, and analyze traces/logs to verify instrumentation behavior.
argument-hint: [build-nuget|deploy|test|logs|trace|configure]
disable-model-invocation: true
allowed-tools: Bash(az:functionapp:show:*) Bash(az:functionapp:list:*) Bash(az:functionapp:list-functions:*) Bash(az:functionapp:function:list:*) Bash(az:functionapp:function:show:*) Bash(az:functionapp:config:appsettings:list:*) Bash(az:functionapp:config:appsettings:set:*) Bash(az:functionapp:config:appsettings:delete:*) Bash(az:functionapp:config:show:*) Bash(az:functionapp:deployment:list:*) Bash(az:functionapp:deployment:show:*) Bash(az:functionapp:deployment:source:show:*) Bash(az:functionapp:plan:list:*) Bash(az:functionapp:plan:show:*) Bash(az:functionapp:restart:*) Bash(az:functionapp:stop:*) Bash(az:functionapp:start:*) Bash(az:webapp:log:download:*) Bash(az:webapp:log:tail:*) Bash(az:group:list:*) Bash(az:group:show:*) Bash(curl:*) Bash(pwsh:*) Bash(func:azure:functionapp:publish:*) Bash(func:azure:functionapp:logstream:*) Bash(func:azure:functionapp:list-functions:*) Bash(func:azure:functionapp:fetch-app-settings:*) Bash(func:azure:functionapp:fetch:*) Bash(dotnet:restore) Bash(dotnet:clean) Bash(dotnet:build:*) Bash(unzip:*) Bash(date:*) Bash(grep:*) Bash(find:*) Bash(ls:*) Bash(cat:*) Bash(head:*) Bash(tail:*) Bash(wc:*) Bash(sort:*) Bash(jq:*) Bash(uname:*) Read
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
- **PowerShell**: See [README.md](README.md#installing-powershell)
- **Azure CLI**: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
- **Azure Functions Core Tools**: https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local
- **.NET SDK**: https://dotnet.microsoft.com/download

## Commands

When invoked with an argument, perform the corresponding workflow:

- `/azure-functions build-nuget` - Build the Datadog.AzureFunctions NuGet package
- `/azure-functions deploy [app-name]` - Deploy to Azure Function App
- `/azure-functions test [app-name]` - Trigger and verify function execution
- `/azure-functions logs [app-name]` - Download and analyze logs
- `/azure-functions trace [trace-id]` - Analyze specific trace in Datadog
- `/azure-functions configure [app-name]` - Configure environment variables for Datadog instrumentation

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
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir>
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
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir>

# First build after new dd-trace-dotnet release (download bundle files once)
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -BuildId 12345 -CopyTo <output-dir>
```

### 2. Deploy Function

**IMPORTANT**: Before deploying, verify that a `nuget.config` file exists in the sample app directory or a parent directory. This file is required for `dotnet restore` to resolve the locally-built `Datadog.AzureFunctions` package from the local NuGet feed.

**Verify nuget.config exists**:
```powershell
$nugetConfig = .\.claude\skills\azure-functions\Find-NuGetConfig.ps1 -StartPath "<path-to-sample-app>"
if (-not $nugetConfig) {
    Write-Error "nuget.config not found in sample app directory or parent directories"
    exit 1
}
Write-Host "Found nuget.config at: $nugetConfig"
```

Use the `Deploy-AzureFunction.ps1` script to automate deployment, wait, and trigger:

```powershell
.\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"
```

**What this does**:
1. Runs `dotnet restore` in the sample app directory
2. Publishes to Azure with `func azure functionapp publish`
3. Waits 2 minutes for worker process to restart
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
$deploy = .\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"
```

### 3. Test Function

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

### 4. Configure Environment Variables

Configure Datadog instrumentation environment variables for an Azure Function App:

**Interactive mode** (recommended):
```bash
/azure-functions configure <app-name>
```

This will:
1. Detect the OS/platform (Linux or Windows)
2. Show current environment variable configuration
3. Ask which variables to configure:
   - **Required only** - Minimum variables needed for instrumentation
   - **Required + Recommended** - Add feature disables and sampling rules
   - **Required + Recommended + Debug** - Add debug logging (for troubleshooting)
   - **Custom** - User selects specific variables
4. Prompt for required values (DD_API_KEY, DD_ENV, etc.)
5. Set the variables using Azure CLI

**Required variables** (must be set):
```bash
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
DD_API_KEY=<your-api-key>
DOTNET_STARTUP_HOOKS=<path-to-compat-dll>
```

**Platform-specific paths**:
- **Linux**:
  - `CORECLR_PROFILER_PATH=/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so`
- **Windows** (requires both 32-bit and 64-bit paths):
  - `CORECLR_PROFILER_PATH_32=C:\home\site\wwwroot\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll`
  - `CORECLR_PROFILER_PATH_64=C:\home\site\wwwroot\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll`

**Manual configuration**:
```bash
# Set multiple variables at once
az functionapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings \
    "CORECLR_ENABLE_PROFILING=1" \
    "CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}" \
    "CORECLR_PROFILER_PATH=/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so" \
    "DD_DOTNET_TRACER_HOME=/home/site/wwwroot/datadog" \
    "DD_API_KEY=<your-api-key>" \
    "DOTNET_STARTUP_HOOKS=/home/site/wwwroot/Datadog.Serverless.Compat.dll"
```

**Complete reference**: See [environment-variables.md](environment-variables.md) for all available variables, including:
- Recommended feature disables (AppSec, CI Visibility, RCM, Agent Feature Polling)
- Debug logging configuration
- Direct log submission

### 5. Download and Analyze Logs

Use the `Get-AzureFunctionLogs.ps1` script to download, extract, and analyze logs:

```powershell
.\tracer\tools\Get-AzureFunctionLogs.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -OutputPath $env:TEMP `
  -ExecutionTimestamp "2026-01-23 17:53:00" `
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
$deploy = .\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"

.\tracer\tools\Get-AzureFunctionLogs.ps1 `
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
```bash
# Check deployment status
az functionapp show --name <app-name> --resource-group <resource-group>

# Restart function app
az functionapp restart --name <app-name> --resource-group <resource-group>
```

### Wrong Tracer Version After Deployment
```bash
# Check all worker initializations
grep "Assembly metadata" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log

# If old version, rebuild from the dd-trace-dotnet repo root (each build gets a unique version, no cache issues)
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose
# Then restore and redeploy the sample app
```

### Traces Not Appearing in Datadog
```bash
# Verify DD_API_KEY is set
az functionapp config appsettings list \
  --name <app-name> \
  --resource-group <resource-group> | grep DD_API_KEY

# Check worker initialization in logs
grep "Datadog Tracer initialized" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

**Environment variables**: Verify all required environment variables are configured correctly. See [environment-variables.md](environment-variables.md) for complete reference.

### Separate Traces (Parenting Issue)
1. Get trace ID from host logs at execution timestamp
2. Search worker logs for same trace ID
3. If not found → worker created separate trace
4. Look for worker spans with `p_id: null` instead of parent IDs matching host spans
5. Enable debug logging: `az functionapp config appsettings set --name <app> --resource-group <resource-group> --settings DD_TRACE_DEBUG=1`
6. Re-test and analyze debug messages about AsyncLocal context flow

## Query Datadog API (Trace Analysis)

Use the PowerShell scripts to query traces and logs from the Datadog API. Requires `DD_API_KEY` and `DD_APPLICATION_KEY` environment variables.

### Get-DatadogTrace.ps1

Retrieve all spans for a trace ID with table, JSON, or hierarchy output:

```powershell
# Table view (default)
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "<trace-id>"

# Hierarchy view — shows span parent-child tree with process tags
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "<trace-id>" -OutputFormat hierarchy

# JSON output for further processing
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "<trace-id>" -OutputFormat json

# Search further back in time (default: 2h)
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "<trace-id>" -TimeRange "1d"
```

**Output includes**: operation name, resource name, span ID, parent ID, process tag (host/worker), duration.

### Get-DatadogLogs.ps1

Query logs from Datadog to correlate with traces:

```powershell
# Search logs for a specific service
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:<app-name>"

# Search with time range and limit
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:<app-name> error" -TimeRange "2h" -Limit 100

# Raw output (one line per log entry)
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:<app-name>" -OutputFormat raw
```

## Configure Command Implementation

When invoked with `/azure-functions configure [app-name]`:

1. **Prompt for app name and resource group** (if not provided)
2. **Detect shell environment** (to handle Git Bash path conversion):
   - Check if running in Git Bash on Windows: `uname -s` contains "MINGW" or "MSYS"
   - If Git Bash: Prefix Azure CLI commands with `MSYS_NO_PATHCONV=1`
   - Otherwise: Use commands without prefix
3. **Detect platform**:
   ```bash
   az functionapp show --name <app-name> --resource-group <resource-group> --query "kind" -o tsv
   ```
   - Look for "linux" or "windows" in the kind string
4. **Show current configuration**:
   ```bash
   az functionapp config appsettings list --name <app-name> --resource-group <resource-group>
   ```
   - Filter for DD_* and CORECLR_* variables
5. **Ask configuration level**:
   - **Required only**: CORECLR_*, DD_DOTNET_TRACER_HOME, DD_API_KEY, DOTNET_STARTUP_HOOKS
   - **Required + Recommended**: Add DD_APPSEC_ENABLED=false, DD_CIVISIBILITY_ENABLED=false, DD_REMOTE_CONFIGURATION_ENABLED=false, DD_AGENT_FEATURE_POLLING_ENABLED=false, DD_TRACE_Process_ENABLED=false, DD_ENV, DD_TRACE_SAMPLING_RULES
   - **Required + Recommended + Debug**: Add DD_TRACE_DEBUG=true, DD_TRACE_LOG_SINKS=file,console-experimental, DD_LOG_LEVEL=debug, DD_LOGS_DIRECT_SUBMISSION_*
   - **Custom**: User selects specific variables
6. **Prompt for values**:
   - DD_API_KEY (required, never show existing value)
   - DD_ENV (optional, show current value if exists)
   - DD_TRACE_SAMPLING_RULES (optional, suggest default)
7. **Set platform-specific paths** based on detected OS:
   - **Linux**: Single `CORECLR_PROFILER_PATH=/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so`
   - **Windows**: Separate 32/64-bit paths:
     - `CORECLR_PROFILER_PATH_32=C:\home\site\wwwroot\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll`
     - `CORECLR_PROFILER_PATH_64=C:\home\site\wwwroot\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll`
8. **Apply settings**:
   - **CRITICAL**: When using Git Bash on Windows, prefix the command with `MSYS_NO_PATHCONV=1` to prevent automatic path conversion (Linux paths like `/home/site/...` would otherwise be converted to `C:/Program Files/Git/home/site/...`)
   - **PowerShell/CMD**: No prefix needed, use command as-is
   ```bash
   # Git Bash (Windows)
   MSYS_NO_PATHCONV=1 az functionapp config appsettings set \
     --name <app-name> \
     --resource-group <resource-group> \
     --settings "KEY1=value1" "KEY2=value2" ...

   # PowerShell/CMD/Linux/macOS
   az functionapp config appsettings set \
     --name <app-name> \
     --resource-group <resource-group> \
     --settings "KEY1=value1" "KEY2=value2" ...
   ```
9. **Confirm success** and remind to restart if needed:
   ```bash
   az functionapp restart --name <app-name> --resource-group <resource-group>
   ```

## Interactive Mode

If invoked without arguments (`/azure-functions`), guide the user through:

1. **Understand the goal**: What are they testing? (New feature, bug fix, trace verification, initial setup)
2. **Check configuration**: Ask if environment variables are configured (offer to run `/azure-functions configure`)
3. **Verify .csproj**: Check that `Datadog.AzureFunctions.csproj` uses PackageReference (not ProjectReference) for local testing (see step 1 above)
4. **Build**: Run Build-AzureFunctionsNuget.ps1
5. **Select app**: Which test app to deploy to?
6. **Verify prerequisites**: Use `Find-NuGetConfig.ps1` to check that the sample app has a `nuget.config` file configured with the local NuGet feed
7. **Deploy**: Navigate to sample app and publish
8. **Wait**: Remind to wait 1-2 minutes for worker restart
9. **Test**: Trigger function and capture timestamp
10. **Download logs**: Pull logs from Azure
11. **Analyze**: Guide through log analysis based on their goal
12. **Verify**: Run through verification checklist
13. **Check .csproj**: Remind that the PackageReference in `Datadog.AzureFunctions.csproj` is for local testing only — DO NOT commit

## Additional Resources

- **Log analysis**: [log-analysis-guide.md](log-analysis-guide.md) - Manual log investigation patterns (when the automated `-All` analysis isn't sufficient)
- **Scripts**: [scripts-reference.md](scripts-reference.md) - Reusable PowerShell scripts and one-liners
- **Environment variables**: [environment-variables.md](environment-variables.md) - Complete reference for Azure Functions configuration

## References

- Detailed docs: `docs/development/AzureFunctions.md`
- Architecture deep dive: `docs/development/for-ai/AzureFunctions-Architecture.md`
