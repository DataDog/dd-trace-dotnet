---
name: azure-functions
description: Build, deploy, and test Azure Functions instrumented with Datadog.AzureFunctions NuGet package. Use when working on Azure Functions integration, deploying to test environments, analyzing traces, or troubleshooting instrumentation issues.
argument-hint: [build|deploy|test|logs|trace]
disable-model-invocation: true
allowed-tools: Bash(az:functionapp:show:*) Bash(az:functionapp:list:*) Bash(az:functionapp:list-functions:*) Bash(az:functionapp:function:list:*) Bash(az:functionapp:function:show:*) Bash(az:functionapp:config:appsettings:list:*) Bash(az:functionapp:config:show:*) Bash(az:functionapp:deployment:list:*) Bash(az:functionapp:deployment:show:*) Bash(az:functionapp:deployment:source:show:*) Bash(az:functionapp:plan:list:*) Bash(az:functionapp:plan:show:*) Bash(az:functionapp:log:download:*) Bash(az:webapp:log:tail:*) Bash(az:group:list:*) Bash(az:group:show:*) Bash(curl:*) Bash(func:azure:functionapp:logstream:*) Bash(func:azure:functionapp:list-functions:*) Bash(func:azure:functionapp:fetch-app-settings:*) Bash(func:azure:functionapp:fetch:*) Bash(dotnet:restore) Bash(dotnet:clean) Bash(dotnet:build:*) Bash(unzip:*) Bash(date:*) Bash(grep:*) Bash(find:*) Bash(ls:*) Bash(cat:*) Bash(head:*) Bash(tail:*) Bash(wc:*) Bash(sort:*) Bash(jq:*) Read
---

# Azure Functions Development Workflow

This skill guides you through building, deploying, and testing Azure Functions with Datadog instrumentation.

## Commands

When invoked with an argument, perform the corresponding workflow:

- `/azure-functions build-nuget` - Build the Datadog.AzureFunctions NuGet package
- `/azure-functions deploy [app-name]` - Deploy to Azure Function App
- `/azure-functions test [app-name]` - Trigger and verify function execution
- `/azure-functions logs [app-name]` - Download and analyze logs
- `/azure-functions trace [trace-id]` - Analyze specific trace in Datadog

If no argument is provided, guide the user through the full workflow interactively.

## Context

**Current repository**: D:\source\datadog\dd-trace-dotnet

**Test Function Apps** (in resource group `lucas.pimentel`, Canada Central):

| Name | Purpose | Runtime | Plan |
|------|---------|---------|------|
| **lucasp-premium-linux-isolated-aspnet** | **Primary** - Isolated .NET 8 with ASP.NET Core | .NET 8 Isolated | Premium |
| lucasp-premium-linux-isolated | Isolated .NET 8 (no ASP.NET Core) | .NET 8 Isolated | Premium |
| lucasp-premium-linux-inproc | In-process .NET 6 | .NET 6 In-Process | Premium |
| lucasp-premium-windows-isolated-aspnet | Windows isolated with ASP.NET Core | .NET 8 Isolated | Premium |
| lucasp-premium-windows-isolated | Windows isolated (no ASP.NET Core) | .NET 8 Isolated | Premium |
| lucasp-premium-windows-inproc | Windows in-process | .NET 6 In-Process | Premium |
| lucasp-consumption-windows-isolated | Windows consumption | .NET 8 Isolated | Consumption |
| lucasp-flex-consumption-isolated | Flex consumption | .NET 8 Isolated | Flex Consumption |

**Sample Applications**:
- Primary: `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore`
- All samples: `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet`

**Temporary Directories**:
- NuGet packages: `D:\temp\nuget`
- Log downloads: `D:\temp\logs-*.zip`
- Trace payloads: `D:\temp\trace_payload_*.json`

## Workflow Steps

### 1. Build NuGet Package

Build the `Datadog.AzureFunctions` NuGet package with your changes:

```powershell
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo D:\temp\nuget -Verbose
```

**What this does**:
1. Generates a unique prerelease version from a timestamp (e.g. `3.38.0-dev.20260209.143022`)
2. Cleans previous builds
3. Builds `Datadog.Trace` (net6.0 and net461)
4. Publishes to bundle folder
5. Packages `Datadog.AzureFunctions.nupkg` with the generated version
6. Copies to `D:\temp\nuget`

**Versioning**: Each build gets a unique version, so NuGet caching is never an issue.
The sample app in `serverless-dev-apps` uses a floating version `3.38.0-dev.*` in
`Directory.Packages.props` to always resolve the latest local dev build.

**Options**:
- `-Version '3.38.0-dev.custom'` - Use a specific version instead of auto-generating
- `-BuildId 12345` - Download bundle from Azure DevOps build first

**Alternative**: Download bundle from Azure DevOps build:
```powershell
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -BuildId 12345 -CopyTo D:\temp\nuget -Verbose
```

### 2. Deploy and Test Function

Use the `Deploy-AzureFunction.ps1` script to automate deployment, wait, and trigger:

```powershell
.\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "lucasp-premium-linux-isolated-aspnet" `
  -ResourceGroup "lucas.pimentel" `
  -SampleAppPath "D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore" `
  -Verbose
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

**Default app**: If no app name specified, use `lucasp-premium-linux-isolated-aspnet`

**Pipeline usage** (save output for log analysis):
```powershell
$deploy = .\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "lucasp-premium-linux-isolated-aspnet" `
  -ResourceGroup "lucas.pimentel" `
  -SampleAppPath "D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore"
```

### 3. Download and Analyze Logs

Use the `Get-AzureFunctionLogs.ps1` script to download, extract, and analyze logs:

```powershell
.\tracer\tools\Get-AzureFunctionLogs.ps1 `
  -AppName "lucasp-premium-linux-isolated-aspnet" `
  -ResourceGroup "lucas.pimentel" `
  -ExecutionTimestamp "2026-01-23 17:53:00" `
  -All `
  -Verbose
```

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
  -AppName "lucasp-premium-linux-isolated-aspnet" `
  -ResourceGroup "lucas.pimentel" `
  -SampleAppPath "D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore"

.\tracer\tools\Get-AzureFunctionLogs.ps1 `
  -AppName $deploy.AppName `
  -ResourceGroup "lucas.pimentel" `
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
az functionapp show --name lucasp-premium-linux-isolated-aspnet --resource-group lucas.pimentel

# Restart function app
az functionapp restart --name lucasp-premium-linux-isolated-aspnet --resource-group lucas.pimentel
```

### Wrong Tracer Version After Deployment
```bash
# Check all worker initializations
grep "Assembly metadata" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log

# If old version, rebuild (each build gets a unique version, no cache issues)
cd D:/source/datadog/dd-trace-dotnet
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo D:\temp\nuget -Verbose
# Then restore and redeploy the sample app
```

### Traces Not Appearing in Datadog
```bash
# Verify DD_API_KEY is set
az functionapp config appsettings list \
  --name lucasp-premium-linux-isolated-aspnet \
  --resource-group lucas.pimentel | grep DD_API_KEY

# Check worker initialization in logs
grep "Datadog Tracer initialized" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

### Separate Traces (Parenting Issue)
1. Get trace ID from host logs at execution timestamp
2. Search worker logs for same trace ID
3. If not found → worker created separate trace
4. Look for worker spans with `p_id: null` instead of parent IDs matching host spans
5. Enable debug logging: `az functionapp config appsettings set --name <app> --resource-group lucas.pimentel --settings DD_TRACE_DEBUG=1`
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
          "query": "service:lucasp-premium-linux-isolated @aas.function.process:worker",
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
2. **Build**: Run Build-AzureFunctionsNuget.ps1
3. **Select app**: Which test app to deploy to? (default: lucasp-premium-linux-isolated-aspnet)
4. **Deploy**: Navigate to sample app and publish
5. **Wait**: Remind to wait 1-2 minutes for worker restart
6. **Test**: Trigger function and capture timestamp
7. **Download logs**: Pull logs from Azure
8. **Analyze**: Guide through log analysis based on their goal
9. **Verify**: Run through verification checklist

## Additional Resources

For detailed log analysis patterns and grep examples, see [log-analysis-guide.md](log-analysis-guide.md).

For reusable bash/PowerShell scripts and one-liners, see [scripts-reference.md](scripts-reference.md).

## References

- Detailed docs: `docs/development/AzureFunctions.md`
- Architecture deep dive: `docs/development/for-ai/AzureFunctions-Architecture.md`
- Parent CLAUDE.md: `D:\source\datadog\CLAUDE.md` (Azure Functions Testing Workflow section)
