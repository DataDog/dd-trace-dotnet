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
1. Cleans previous builds
2. Removes cached NuGet packages
3. Builds `Datadog.Trace` (net6.0 and net461)
4. Publishes to bundle folder
5. Packages `Datadog.AzureFunctions.nupkg`
6. Copies to `D:\temp\nuget`

**Alternative**: Download bundle from Azure DevOps build:
```powershell
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -BuildId 12345 -CopyTo D:\temp\nuget -Verbose
```

### 2. Deploy to Azure

Navigate to sample app and deploy:

```bash
cd D:/source/datadog/serverless-dev-apps/azure/functions/dotnet/isolated-dotnet8-aspnetcore
dotnet restore
func azure functionapp publish lucasp-premium-linux-isolated-aspnet
```

**IMPORTANT**: Wait 1-2 minutes after deployment for the worker process to restart before testing.

**Default app**: If no app name specified, use `lucasp-premium-linux-isolated-aspnet`

### 3. Test Function

Trigger the HTTP test endpoint:

```bash
curl https://lucasp-premium-linux-isolated-aspnet.azurewebsites.net/api/HttpTest
```

**Note**: Azure URL pattern uses base name without `-aspnet` suffix for this specific app.

**Capture timestamp** when triggering (for log analysis):
```bash
echo "Triggered at $(date -u +%Y-%m-%d\ %H:%M:%S) UTC"
```

### 4. Download and Analyze Logs

**Download logs**:
```bash
az functionapp log download \
  --name lucasp-premium-linux-isolated-aspnet \
  --resource-group lucas.pimentel \
  --log-path D:/temp/logs-$(date +%H%M%S).zip
```

**Extract logs**:
```bash
cd D:/temp
unzip -q -o logs-*.zip
ls -la LogFiles/datadog/
```

**Log file patterns**:
- **Host process**: `dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-{pid}.log`
- **Worker process**: `dotnet-tracer-managed-dotnet-{pid}.log`

**CRITICAL**: Always filter logs by timestamp - never use head/tail on downloaded files:

```bash
# Replace with actual execution timestamp
grep "2026-01-23 17:53:" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

**Why**: Log files are append-only and contain entries from multiple deployments/restarts.

### 5. Verify Tracer Version

Check the worker loaded the expected version:

```bash
# Find most recent initialization
grep "Assembly metadata" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log | tail -1

# Verify version in recent logs
grep "2026-01-23 17:53:" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log | grep "TracerVersion" | head -1
```

### 6. Analyze Trace Context Flow

**Find host trace ID**:
```bash
grep "2026-01-23 17:53:39" LogFiles/datadog/dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log | grep "Span started"
```

**Check worker spans use same trace ID**:
```bash
# Replace with actual trace ID from above
grep "68e948220000000047fef7bad8bb854e" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

**Span fields**:
- **s_id** (span ID): Unique identifier for this span
- **p_id** (parent ID): Span ID of parent span (`null` = root span)
- **t_id** (trace ID): Trace this span belongs to

**Healthy trace**: Worker spans have same `t_id` as host and `p_id` matching host span IDs
**Broken trace**: Worker spans have different `t_id` or `p_id: null` (orphaned root)

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

# If old version, clear NuGet cache and rebuild
dotnet nuget locals all --clear
cd D:/source/datadog/dd-trace-dotnet
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo D:\temp\nuget -Verbose
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
