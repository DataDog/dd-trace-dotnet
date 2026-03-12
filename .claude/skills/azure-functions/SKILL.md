---
name: azure-functions
description: Build the .NET tracer locally, deploy it to a test Azure Function App, trigger it, and analyze traces/logs to verify instrumentation. Use whenever working on Azure Functions: building/deploying the tracer, analyzing logs or spans, or configuring Datadog environment variables on Azure.
argument-hint: [deploy|test|logs|configure|build-nuget]
allowed-tools: Bash(pwsh *) Bash(az functionapp show *) Bash(az functionapp list *) Bash(az functionapp list-functions *) Bash(az functionapp function list *) Bash(az functionapp function show *) Bash(az functionapp config appsettings list *) Bash(az functionapp config appsettings set *) Bash(az functionapp config appsettings delete *) Bash(az functionapp config show *) Bash(az functionapp deployment list *) Bash(az functionapp deployment show *) Bash(az functionapp deployment source show *) Bash(az functionapp deployment source config-zip *) Bash(az functionapp plan list *) Bash(az functionapp plan show *) Bash(az functionapp restart *) Bash(az functionapp stop *) Bash(az functionapp start *) Bash(az webapp log download *) Bash(az webapp log tail *) Bash(az group list *) Bash(az group show *) Bash(curl *) Bash(func azure functionapp logstream *) Bash(func azure functionapp list-functions *) Bash(func azure functionapp fetch-app-settings *) Bash(func azure functionapp fetch *) Bash(dotnet restore) Bash(dotnet clean) Bash(dotnet build *) Bash(dotnet publish *) Bash(unzip *) Read
---

# Azure Functions Dev/Test Workflow

This skill helps tracer engineers test changes to the managed tracer (`Datadog.Trace.dll`) in Azure Functions: build the tracer locally, publish a sample app that uses the released `Datadog.AzureFunctions` NuGet package, swap in the locally-built `Datadog.Trace.dll`, deploy to Azure, trigger the function, and analyze traces/logs to verify instrumentation behavior.

## Prerequisites

This skill requires the following tools (assume they are installed and only troubleshoot if errors occur):

- **PowerShell**: `pwsh` (PowerShell 7+) preferred, or `powershell.exe` (PowerShell 5.1+ on Windows)
  - Always prefer `pwsh` over `powershell.exe` when available
  - Scripts use PowerShell-specific cmdlets like `Expand-Archive`
- **Azure CLI**: `az` (must be authenticated)
- **.NET SDK**: Matching target framework of sample app

**Optional tools** (not required for the main workflow):
- **Azure Functions Core Tools**: `func` (useful for debugging, listing functions, streaming logs)

**Only if a tool fails, provide installation links**:
- **PowerShell**: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell
- **Azure CLI**: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
- **Azure Functions Core Tools**: https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local
- **.NET SDK**: https://dotnet.microsoft.com/download

## Commands

When invoked with an argument, perform the corresponding workflow:

- `/azure-functions deploy [app-name]` - Deploy to Azure Function App (build tracer, publish app, swap DLL, zip deploy)
- `/azure-functions configure [app-name]` - Configure environment variables for Datadog instrumentation
- `/azure-functions test [app-name]` - Trigger and verify function execution
- `/azure-functions logs [app-name]` - Download and analyze logs
- `/azure-functions build-nuget` - Build the Datadog.AzureFunctions NuGet package (for NuGet package testing only)

If no argument is provided, guide the user through the full workflow interactively.

## Context

**Current repository**: This skill assumes you are working from the root of the `dd-trace-dotnet` repository.

**Sample app requirements**: The sample app must:
1. Reference the released `Datadog.AzureFunctions` NuGet package from nuget.org (pinned version)
2. NOT need a local NuGet feed or special `nuget.config` — the standard nuget.org feed is sufficient
3. The local NuGet feed at `apm-serverless-test-apps/.../nuget/local-source` is no longer used in the main workflow

**How it works**: The deploy script publishes the sample app (which includes the released `Datadog.AzureFunctions` package and all its bundled files), then replaces only `datadog/net6.0/Datadog.Trace.dll` in the publish output with a locally-built version before zipping and deploying to Azure.

## Workflow Steps

### 1. Deploy Function

Use the `Deploy-AzureFunction.ps1` script to build, publish, swap the tracer DLL, and deploy:

```powershell
./tracer/tools/Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"
```

See [scripts-reference.md](scripts-reference.md#deploy-azurefunctionps1) for all parameters and details.

**Verify environment variables are configured** (skip if already done on a previous deploy):
```powershell
$envCheck = ./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "<app-name>" -ResourceGroup "<resource-group>"
if (-not $envCheck.AllRequiredPresent) {
    Write-Warning "Required environment variables are missing. Run '/azure-functions configure' first, or proceed if you plan to configure after deploying."
}
```

**Pipeline usage** (save output for log analysis):
```powershell
$deploy = ./tracer/tools/Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-sample-app>"
```

### 2. Configure Environment Variables

Configure Datadog instrumentation environment variables for an Azure Function App using `Set-EnvVars.ps1`:

```powershell
./.claude/skills/azure-functions/Set-EnvVars.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -ApiKey "<api-key>" `
  -Tier recommended `
  -Env "dev-lucas"
```

**Tiers**: `required` (minimum for instrumentation), `recommended` (+ feature disables), `debug` (+ debug logging). See [scripts-reference.md](scripts-reference.md#set-envvarsps1) for all parameters, tiers, and details.

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

**Note**: The Deploy script (step 1) already triggers and captures a timestamp. Use this step when you want to re-test without redeploying.

### 4. Download and Analyze Logs

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

See [scripts-reference.md](scripts-reference.md#get-azurefunctionlogsps1) for all parameters and details.

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

### 5. Build NuGet Package (Optional - for NuGet package testing only)

This step is only needed when testing changes to the `Datadog.AzureFunctions` NuGet package structure itself. For testing tracer code changes, skip this step and use the Deploy workflow above.

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

See [scripts-reference.md](scripts-reference.md#build-azurefunctionsnugetps1) for all parameters and details.

## Verification Checklist

After deployment and testing:

- [ ] Function responds successfully (HTTP 200)
- [ ] Worker loaded correct tracer version (check "Assembly metadata")
- [ ] Host and worker spans share same trace ID
- [ ] Span parent-child relationships are correct (check `p_id` → `s_id` links)
- [ ] Process tags are correct (`aas.function.process:host` or `worker`)
- [ ] No error logs at execution timestamp
- [ ] AsyncLocal context flows correctly (if debugging context issues)

## Additional Resources

- **Troubleshooting**: [troubleshooting.md](troubleshooting.md) - Common issues: function not responding, deployment failures, wrong tracer version, missing traces, parenting issues
- **Datadog API**: [datadog-api.md](datadog-api.md) - Query traces and logs from Datadog (Get-DatadogTrace.ps1, Get-DatadogLogs.ps1)
- **Log analysis**: [log-analysis-guide.md](log-analysis-guide.md) - Manual log investigation patterns (when the automated `-All` analysis isn't sufficient)
- **Scripts**: [scripts-reference.md](scripts-reference.md) - Reusable PowerShell scripts and one-liners
- **Environment variables**: [environment-variables.md](environment-variables.md) - Complete reference for Azure Functions configuration

## References

- Detailed docs: `docs/development/AzureFunctions.md`
- Architecture deep dive: `docs/development/for-ai/AzureFunctions-Architecture.md`
