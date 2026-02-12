---
name: azure-functions
description: Build, deploy, and test Azure Functions instrumented with Datadog.AzureFunctions NuGet package. Use when working on Azure Functions integration, deploying to test environments, analyzing traces, or troubleshooting instrumentation issues.
argument-hint: [build|deploy|test|logs|trace]
disable-model-invocation: true
allowed-tools: Bash(az:functionapp:show:*) Bash(az:functionapp:list:*) Bash(az:functionapp:list-functions:*) Bash(az:functionapp:function:list:*) Bash(az:functionapp:function:show:*) Bash(az:functionapp:config:appsettings:list:*) Bash(az:functionapp:config:show:*) Bash(az:functionapp:deployment:list:*) Bash(az:functionapp:deployment:show:*) Bash(az:functionapp:deployment:source:show:*) Bash(az:functionapp:plan:list:*) Bash(az:functionapp:plan:show:*) Bash(az:functionapp:log:download:*) Bash(az:webapp:log:tail:*) Bash(az:group:list:*) Bash(az:group:show:*) Bash(curl:*) Bash(func:azure:functionapp:logstream:*) Bash(func:azure:functionapp:list-functions:*) Bash(func:azure:functionapp:fetch-app-settings:*) Bash(func:azure:functionapp:fetch:*) Bash(dotnet:restore) Bash(dotnet:clean) Bash(dotnet:build:*) Bash(unzip:*) Bash(date:*) Bash(grep:*) Bash(find:*) Bash(ls:*) Bash(cat:*) Bash(head:*) Bash(tail:*) Bash(wc:*) Bash(sort:*) Bash(jq:*) Read
---

# Azure Functions Development Workflow

This skill guides you through building, deploying, and testing Azure Functions with Datadog instrumentation.

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

**CRITICAL**: Before building, temporarily modify `tracer/src/Datadog.AzureFunctions/Datadog.AzureFunctions.csproj` to use package references instead of project references. This ensures the locally-built package references the latest releases from nuget.org:

```xml
<!-- Replace these ProjectReference lines: -->
<ProjectReference Include="$(MSBuildThisFileDirectory)..\Datadog.Trace.Manual\Datadog.Trace.Manual.csproj" />
<ProjectReference Include="$(MSBuildThisFileDirectory)..\Datadog.Trace.Annotations\Datadog.Trace.Annotations.csproj" />

<!-- With these PackageReference lines: -->
<PackageReference Include="Datadog.Trace" Version="*"/>
<PackageReference Include="Datadog.Trace.Annotations" Version="*" />
```

**IMPORTANT**: This is a temporary change for local testing only. Do NOT commit this change.

Build the `Datadog.AzureFunctions` NuGet package with your changes:

```powershell
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir>
```

**What this does**:
1. (If `-BuildId` specified and files not already downloaded) Downloads bundle from Azure DevOps build once
2. Generates a unique prerelease version from a timestamp (e.g. `3.38.0-dev20260209143022`)
3. Cleans previous builds
4. Builds `Datadog.Trace` (net6.0 and net461)
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

### 2. Deploy and Test Function

**IMPORTANT**: Before deploying, verify that a `nuget.config` file exists in the sample app directory or a parent directory. This file is required for `dotnet restore` to resolve the locally-built `Datadog.AzureFunctions` package from the local NuGet feed.

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

### 3. Download and Analyze Logs

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

### Separate Traces (Parenting Issue)
1. Get trace ID from host logs at execution timestamp
2. Search worker logs for same trace ID
3. If not found → worker created separate trace
4. Look for worker spans with `p_id: null` instead of parent IDs matching host spans
5. Enable debug logging: `az functionapp config appsettings set --name <app> --resource-group <resource-group> --settings DD_TRACE_DEBUG=1`
6. Re-test and analyze debug messages about AsyncLocal context flow

## Query Datadog API (Advanced)

**Search for spans by service and process type**:
```bash
curl -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "service:<app-name> @aas.function.process:worker",
          "from": "now-10m",
          "to": "now"
        }
      },
      "type": "search_request"
    }
  }'
```

## Interactive Mode

If invoked without arguments (`/azure-functions`), guide the user through:

1. **Understand the goal**: What are they testing? (New feature, bug fix, trace verification)
2. **Modify .csproj**: Temporarily change `Datadog.AzureFunctions.csproj` to use PackageReference instead of ProjectReference (see step 1 above)
3. **Build**: Run Build-AzureFunctionsNuget.ps1
4. **Select app**: Which test app to deploy to?
5. **Verify prerequisites**: Check that the sample app has a `nuget.config` file configured with the local NuGet feed
6. **Deploy**: Navigate to sample app and publish
7. **Wait**: Remind to wait 1-2 minutes for worker restart
8. **Test**: Trigger function and capture timestamp
9. **Download logs**: Pull logs from Azure
10. **Analyze**: Guide through log analysis based on their goal
11. **Verify**: Run through verification checklist
12. **Revert .csproj**: Remind to revert the temporary change to `Datadog.AzureFunctions.csproj` (DO NOT commit)

## Additional Resources

For detailed log analysis patterns and grep examples, see [log-analysis-guide.md](log-analysis-guide.md).

For reusable bash/PowerShell scripts and one-liners, see [scripts-reference.md](scripts-reference.md).

## References

- Detailed docs: `docs/development/AzureFunctions.md`
- Architecture deep dive: `docs/development/for-ai/AzureFunctions-Architecture.md`
