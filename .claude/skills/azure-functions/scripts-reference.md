# Azure Functions Testing Scripts

Quick reference scripts and commands for Azure Functions development workflow.

## PowerShell Scripts (tracer/tools/)

**Prerequisites**: PowerShell 5.1+ is required for all scripts in this section.
- **Recommended**: PowerShell 7+ (`pwsh`) for cross-platform support
- **Minimum**: PowerShell 5.1 (`powershell.exe` on Windows)
- Verify: `pwsh -Version` or `powershell -NoProfile -Command '$PSVersionTable.PSVersion'`

**Note**: These scripts use PowerShell-specific cmdlets (e.g., `Expand-Archive`, `Invoke-RestMethod`) that cannot be easily replicated in bash. Always prefer `pwsh` over `powershell.exe` when both are available.

### Find-NuGetConfig.ps1

Searches for `nuget.config` file by walking up the directory hierarchy from a starting path.

**Location**: `.claude/skills/azure-functions/Find-NuGetConfig.ps1`

**Basic usage**:
```powershell
$nugetConfig = .\.claude\skills\azure-functions\Find-NuGetConfig.ps1 -StartPath "<path-to-sample-app>"
if (-not $nugetConfig) {
    Write-Error "nuget.config not found in sample app directory or parent directories"
    exit 1
}
Write-Host "Found nuget.config at: $nugetConfig"
```

**Parameters**:
- `-StartPath` - Directory to start searching from (defaults to current directory)

**Output**: Returns the full path to `nuget.config` if found, otherwise returns `$null`

**Use case**: Verify that a sample app has access to a `nuget.config` before deploying. The `nuget.config` file defines the local NuGet feed where the locally-built `Datadog.AzureFunctions` package is stored.

### Deploy-AzureFunction.ps1

Automates deployment, wait, and HTTP trigger with timestamp capture.

**Basic usage**:
```powershell
.\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-your-azure-functions-app>" `
  -Verbose
```

**Pipeline usage** (save output for log analysis):
```powershell
$deploy = .\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-your-azure-functions-app>"

# Use $deploy.ExecutionTimestamp and $deploy.AppName for log analysis
```

**Options**:
- `-SkipBuild` - Skip `dotnet restore`
- `-SkipWait` - Skip 2-minute wait
- `-WaitSeconds 60` - Custom wait duration
- `-SkipTrigger` - Skip HTTP trigger
- `-TriggerUrl "https://..."` - Custom trigger URL

**Output**: PSCustomObject with `AppName`, `ExecutionTimestamp`, `TriggerUrl`, `HttpStatus`

### Get-AzureFunctionLogs.ps1

Downloads, extracts, and analyzes Azure Function logs.

**Basic usage**:
```powershell
.\tracer\tools\Get-AzureFunctionLogs.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -OutputPath $env:TEMP `
  -ExecutionTimestamp "2026-01-23 17:53:00" `
  -All `
  -Verbose
```

**Full pipeline** (deploy + analyze):
```powershell
$deploy = .\tracer\tools\Deploy-AzureFunction.ps1 `
  -AppName "<app-name>" `
  -ResourceGroup "<resource-group>" `
  -SampleAppPath "<path-to-your-azure-functions-app>"

.\tracer\tools\Get-AzureFunctionLogs.ps1 `
  -AppName $deploy.AppName `
  -ResourceGroup "<resource-group>" `
  -OutputPath $env:TEMP `
  -ExecutionTimestamp $deploy.ExecutionTimestamp `
  -All
```

**Analysis options**:
- `-ShowVersion` - Display Datadog tracer version
- `-ShowSpans` - Count spans at execution timestamp
- `-CheckParenting` - Validate trace parenting
- `-All` - Enable all analysis
- `-OutputPath "<output-dir>"` - Custom output directory

**Output**: PSCustomObject with `LogZipPath`, `ExtractDir`, `DatadogLogDir`, `TracerVersion`, `SpanCount`, `ParentingAnalysis`

### Build-AzureFunctionsNuget.ps1

Build the Datadog.AzureFunctions NuGet package.

**Usage**:
```powershell
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose
```

**Options**:
- `-BuildId 12345` - Download bundle from Azure DevOps build instead of building locally
- `-CopyTo <output-dir>` - Copy package to directory
- `-Verbose` - Show detailed build output

## Build Scripts

### Build NuGet Package (Standard)
```powershell
# Build with verbose output
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose

# Build without copying
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -Verbose

# Build from specific Azure DevOps build
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -BuildId 12345 -CopyTo <output-dir> -Verbose
```

### Clean and Rebuild
```powershell
# Clear NuGet caches and rebuild
dotnet nuget locals all --clear
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose
```

## Testing Scripts

### Trigger and Capture Timestamp
```bash
APP_NAME="<app-name>"

# Trigger with timestamp
echo "=== Triggering at $(date -u +%Y-%m-%d\ %H:%M:%S) UTC ==="
TIMESTAMP=$(date -u +%Y-%m-%d\ %H:%M:%S)

curl https://${APP_NAME}.azurewebsites.net/api/HttpTest

echo "Triggered at: $TIMESTAMP"
echo "Use this timestamp for log filtering: grep \"$TIMESTAMP\" worker.log"
```

### Multiple Test Executions
```bash
APP_NAME="<app-name>"

for i in {1..5}; do
  echo "=== Execution $i at $(date -u +%Y-%m-%d\ %H:%M:%S) UTC ==="
  curl -s https://${APP_NAME}.azurewebsites.net/api/HttpTest
  echo ""
  sleep 10
done
```

### Test with Different Endpoints
```bash
APP_NAME="<app-name>"
BASE_URL="https://${APP_NAME}.azurewebsites.net"

# Test multiple functions
for FUNC in HttpTest TimerTest QueueTest; do
  echo "Testing $FUNC..."
  curl -s "${BASE_URL}/api/${FUNC}"
  sleep 5
done
```

## Log Management Scripts

### Download and Extract Logs
```bash
APP_NAME="<app-name>"
RESOURCE_GROUP="<resource-group>"
TIMESTAMP=$(date +%H%M%S)
LOG_FILE="<output-dir>/logs-${TIMESTAMP}.zip"

# Download
az webapp log download \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --log-file $LOG_FILE

# Extract
unzip -q -o $LOG_FILE -d <output-dir>/LogFiles-${TIMESTAMP}

echo "Logs extracted to: <output-dir>/LogFiles-${TIMESTAMP}/LogFiles/datadog/"
```

### Download Logs from All Test Apps
```bash
RESOURCE_GROUP="<resource-group>"
APPS=(
  "<app-name>"
  "<app-name-2>"
  "<app-name-3>"
)

for APP in "${APPS[@]}"; do
  echo "Downloading logs from $APP..."
  az webapp log download \
    --name $APP \
    --resource-group $RESOURCE_GROUP \
    --log-file "<output-dir>/logs-${APP}.zip"
done
```

### Auto-Download After Test
```bash
#!/bin/bash
# test-and-download.sh - Trigger function and download logs

APP_NAME="${1:-<app-name>}"
RESOURCE_GROUP="<resource-group>"

# Trigger
EXEC_TIME=$(date -u +%Y-%m-%d\ %H:%M:%S)
echo "Triggering at $EXEC_TIME UTC"
curl https://${APP_NAME}.azurewebsites.net/api/HttpTest

# Wait for logs
echo "Waiting 10 seconds for logs..."
sleep 10

# Download
TIMESTAMP=$(date +%H%M%S)
LOG_FILE="<output-dir>/logs-${TIMESTAMP}.zip"
az webapp log download \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --log-file $LOG_FILE

# Extract
unzip -q -o $LOG_FILE -d <output-dir>/LogFiles-${TIMESTAMP}

echo ""
echo "=== Summary ==="
echo "Execution time: $EXEC_TIME UTC"
echo "Logs location: <output-dir>/LogFiles-${TIMESTAMP}/LogFiles/datadog/"
echo ""
echo "Next steps:"
echo "  cd <output-dir>/LogFiles-${TIMESTAMP}/LogFiles/datadog"
echo "  grep \"$EXEC_TIME\" dotnet-tracer-managed-dotnet-*.log"
```

## Log Analysis Scripts

### Quick Trace Analysis
```bash
#!/bin/bash
# analyze-trace.sh - Quick trace analysis

LOG_DIR="${1:-<output-dir>/LogFiles/LogFiles/datadog}"
EXEC_TIME="${2:-$(date -u +%Y-%m-%d\ %H:%M)}"

cd "$LOG_DIR"

echo "=== Finding execution at $EXEC_TIME ==="

# Get trace ID from host
echo "Host trace ID:"
TRACE_ID=$(grep "$EXEC_TIME" dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log \
  | grep "Span started" \
  | head -1 \
  | grep -o 't_id: [^]]*' \
  | cut -d' ' -f2)

echo "$TRACE_ID"

# Check worker has same trace ID
echo ""
echo "Worker spans in this trace:"
grep "$TRACE_ID" dotnet-tracer-managed-dotnet-*.log | wc -l

# Show all spans
echo ""
echo "=== All spans in trace ==="
grep "$TRACE_ID" dotnet-tracer-managed-*.log | grep "Span started"
```

### Verify Worker Version
```bash
#!/bin/bash
# verify-version.sh - Check worker tracer version

LOG_DIR="${1:-<output-dir>/LogFiles/LogFiles/datadog}"

cd "$LOG_DIR"

echo "=== Most Recent Worker Initialization ==="
grep "Assembly metadata" dotnet-tracer-managed-dotnet-*.log | tail -1

echo ""
echo "=== TracerVersion in Recent Logs ==="
RECENT_TIME=$(date -u +%Y-%m-%d\ %H:%M)
grep "$RECENT_TIME" dotnet-tracer-managed-dotnet-*.log | grep "TracerVersion" | tail -1
```

### Find Span Parenting Issues
```bash
#!/bin/bash
# check-parenting.sh - Find orphaned spans

LOG_DIR="${1:-<output-dir>/LogFiles/LogFiles/datadog}"
EXEC_TIME="${2}"

cd "$LOG_DIR"

echo "=== Root Spans (should only be in host) ==="
echo ""
echo "Host root spans:"
grep "$EXEC_TIME" dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log \
  | grep "Span started" \
  | grep "p_id: null" \
  | wc -l

echo "Worker root spans (should be 0):"
grep "$EXEC_TIME" dotnet-tracer-managed-dotnet-*.log \
  | grep "Span started" \
  | grep "p_id: null" \
  | wc -l

echo ""
echo "=== Unique Trace IDs (should be 1) ==="
grep "$EXEC_TIME" dotnet-tracer-managed-*.log \
  | grep "Span started" \
  | grep -o 't_id: [^]]*' \
  | cut -d' ' -f2 \
  | sort -u \
  | wc -l
```

## Azure CLI Management

### App Settings Management
```bash
APP_NAME="<app-name>"
RESOURCE_GROUP="<resource-group>"

# List all settings
az functionapp config appsettings list \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Get specific setting
az functionapp config appsettings list \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  | jq '.[] | select(.name=="DD_TRACE_DEBUG")'

# Enable debug logging
az functionapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings DD_TRACE_DEBUG=1

# Disable debug logging
az functionapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings DD_TRACE_DEBUG=0
```

### App Lifecycle Management
```bash
APP_NAME="<app-name>"
RESOURCE_GROUP="<resource-group>"

# Show app info
az functionapp show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Restart app
az functionapp restart \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Stop app
az functionapp stop \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Start app
az functionapp start \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP
```

### Deployment History
```bash
APP_NAME="<app-name>"
RESOURCE_GROUP="<resource-group>"

# List recent deployments
az functionapp deployment list \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[].{id:id, status:status, active:active, startTime:startTime}" \
  --output table

# Get most recent deployment
az functionapp deployment list \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[0]"
```

### Stream Live Logs
```bash
APP_NAME="<app-name>"

# Stream application logs
func azure functionapp logstream $APP_NAME

# Stream with Azure CLI (alternative)
az webapp log tail \
  --name $APP_NAME \
  --resource-group <resource-group>
```

## Datadog API Scripts

### Get-DatadogTrace.ps1

Retrieves all spans for a trace ID from the Datadog API. Requires `DD_API_KEY` and `DD_APPLICATION_KEY` environment variables.

**Location**: `tracer/tools/Get-DatadogTrace.ps1`

**Parameters**:
- `-TraceId` (required) - 128-bit trace ID (hex string, e.g., `"690507fc00000000b882bcd2bdac6b9e"`)
- `-TimeRange` (default: `"2h"`) - How far back to search (e.g., `"15m"`, `"1h"`, `"1d"`)
- `-OutputFormat` (default: `"table"`) - Output format: `table`, `json`, or `hierarchy`

**Examples**:
```powershell
# Table view (default) — columns: operation, resource, span ID, parent ID, process, duration
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e"

# Hierarchy view — shows span parent-child tree with process tags
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -OutputFormat hierarchy

# JSON output for further processing
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -OutputFormat json

# Search further back in time
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -TimeRange "1d"
```

### Get-DatadogLogs.ps1

Queries logs from the Datadog Logs API. Requires `DD_API_KEY` and `DD_APPLICATION_KEY` environment variables.

**Location**: `tracer/tools/Get-DatadogLogs.ps1`

**Parameters**:
- `-Query` (required) - Log query (e.g., `"service:my-service error"`)
- `-TimeRange` (default: `"1h"`) - How far back to search (e.g., `"15m"`, `"2h"`, `"1d"`)
- `-Limit` (default: `50`, max: `1000`) - Maximum number of log entries to return
- `-OutputFormat` (default: `"table"`) - Output format: `table`, `json`, or `raw`

**Examples**:
```powershell
# Search logs for a specific service
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:lucasp-premium-linux-isolated"

# Search for errors with time range
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-app error" -TimeRange "2h" -Limit 100

# Raw output (one line per log entry, good for piping)
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-app" -OutputFormat raw
```
