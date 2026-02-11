# Azure Functions Testing Scripts

Quick reference scripts and commands for Azure Functions development workflow.

## PowerShell Scripts (tracer/tools/)

**Prerequisites**: PowerShell 5.1+ is required for all scripts in this section.
- **Recommended**: PowerShell 7+ (`pwsh`) for cross-platform support
- **Minimum**: PowerShell 5.1 (`powershell.exe` on Windows)
- Verify: `pwsh -Version` or `powershell -NoProfile -Command '$PSVersionTable.PSVersion'`

**Note**: These scripts use PowerShell-specific cmdlets (e.g., `Expand-Archive`, `Invoke-RestMethod`) that cannot be easily replicated in bash. Always prefer `pwsh` over `powershell.exe` when both are available.

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

## Deployment Scripts

### Deploy to Primary Test App
```bash
cd <path-to-your-azure-functions-app>
dotnet restore
func azure functionapp publish <app-name>
```

### Deploy to Specific App
```bash
APP_NAME="<app-name>"
cd <path-to-your-azure-functions-app>
dotnet restore
func azure functionapp publish $APP_NAME
```

### Full Clean Deploy
```bash
APP_NAME="<app-name>"
cd <path-to-your-azure-functions-app>

# Clean build artifacts
dotnet clean
rm -rf bin/ obj/

# Restore and deploy
dotnet restore
func azure functionapp publish $APP_NAME

echo "Waiting 2 minutes for worker restart..."
sleep 120
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

### Query Spans
```bash
#!/bin/bash
# query-spans.sh - Query spans from Datadog

SERVICE="${1}"  # Azure Function App name (used as Datadog service name)
PROCESS="${2:-worker}"  # host or worker

curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d "{
    \"data\": {
      \"attributes\": {
        \"filter\": {
          \"query\": \"service:${SERVICE} @aas.function.process:${PROCESS}\",
          \"from\": \"now-10m\",
          \"to\": \"now\"
        }
      },
      \"type\": \"search_request\"
    }
  }" | jq '.data[] | {span_id: .attributes.attributes.span_id, operation: .attributes.attributes.operation_name, process: .attributes.tags."aas.function.process"}'
```

### Query Trace
```bash
#!/bin/bash
# query-trace.sh - Get full trace by ID

TRACE_ID="${1}"

curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d "{
    \"data\": {
      \"attributes\": {
        \"filter\": {
          \"query\": \"@trace_id:${TRACE_ID}\",
          \"from\": \"now-30m\",
          \"to\": \"now\"
        }
      },
      \"type\": \"search_request\"
    }
  }" | jq '.data[] | {span_id, parent_id, operation_name, process: .attributes.tags."aas.function.process"}' | tee "<output-dir>/trace_${TRACE_ID}.json"
```

## Complete Workflow Scripts

### Full Test Cycle
```bash
#!/bin/bash
# full-test-cycle.sh - Complete build, deploy, test, analyze workflow

set -e  # Exit on error

APP_NAME="${1:-<app-name>}"
RESOURCE_GROUP="<resource-group>"

echo "=== 1. Building NuGet Package ==="
# Run from the dd-trace-dotnet repo root
pwsh -NoProfile -Command ".\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose"

echo ""
echo "=== 2. Deploying to $APP_NAME ==="
cd <path-to-your-azure-functions-app>
dotnet restore
func azure functionapp publish $APP_NAME

echo ""
echo "=== 3. Waiting for worker restart (2 minutes) ==="
sleep 120

echo ""
echo "=== 4. Triggering function ==="
EXEC_TIME=$(date -u +%Y-%m-%d\ %H:%M:%S)
echo "Execution time: $EXEC_TIME UTC"
curl https://${APP_NAME}.azurewebsites.net/api/HttpTest
echo ""

echo ""
echo "=== 5. Waiting for logs (10 seconds) ==="
sleep 10

echo ""
echo "=== 6. Downloading logs ==="
TIMESTAMP=$(date +%H%M%S)
LOG_FILE="<output-dir>/logs-${TIMESTAMP}.zip"
az webapp log download \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --log-file $LOG_FILE

unzip -q -o $LOG_FILE -d <output-dir>/LogFiles-${TIMESTAMP}

echo ""
echo "=== 7. Analyzing logs ==="
LOG_DIR="<output-dir>/LogFiles-${TIMESTAMP}/LogFiles/datadog"
cd "$LOG_DIR"

echo "Worker version:"
grep "Assembly metadata" dotnet-tracer-managed-dotnet-*.log | tail -1

echo ""
echo "Spans at execution time:"
grep "$EXEC_TIME" dotnet-tracer-managed-*.log | grep "Span started" | wc -l

echo ""
echo "=== Complete! ==="
echo "Execution time: $EXEC_TIME UTC"
echo "Logs location: $LOG_DIR"
echo ""
echo "Next: Analyze logs with:"
echo "  cd $LOG_DIR"
echo "  grep \"$EXEC_TIME\" dotnet-tracer-managed-dotnet-*.log"
```

### Compare Before/After
```bash
#!/bin/bash
# compare-versions.sh - Test before and after changes

APP_NAME="${1:-<app-name>}"
RESOURCE_GROUP="<resource-group>"

echo "=== Testing BEFORE changes ==="
BEFORE_TIME=$(date -u +%Y-%m-%d\ %H:%M:%S)
curl https://${APP_NAME}.azurewebsites.net/api/HttpTest
sleep 10

BEFORE_LOG="<output-dir>/logs-before.zip"
az webapp log download --name $APP_NAME --resource-group $RESOURCE_GROUP --log-file $BEFORE_LOG
unzip -q -o $BEFORE_LOG -d <output-dir>/before

echo ""
echo "=== Deploying changes ==="
# Run from the dd-trace-dotnet repo root
pwsh -NoProfile -Command ".\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose"

cd <path-to-your-azure-functions-app>
dotnet restore
func azure functionapp publish $APP_NAME

echo "Waiting 2 minutes for restart..."
sleep 120

echo ""
echo "=== Testing AFTER changes ==="
AFTER_TIME=$(date -u +%Y-%m-%d\ %H:%M:%S)
curl https://${APP_NAME}.azurewebsites.net/api/HttpTest
sleep 10

AFTER_LOG="<output-dir>/logs-after.zip"
az webapp log download --name $APP_NAME --resource-group $RESOURCE_GROUP --log-file $AFTER_LOG
unzip -q -o $AFTER_LOG -d <output-dir>/after

echo ""
echo "=== Comparison ==="
echo "Before execution: $BEFORE_TIME UTC"
echo "After execution: $AFTER_TIME UTC"
echo ""
echo "Before logs: <output-dir>/before/LogFiles/datadog/"
echo "After logs: <output-dir>/after/LogFiles/datadog/"
echo ""
echo "Compare with:"
echo "  grep \"$BEFORE_TIME\" <output-dir>/before/LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log > before.txt"
echo "  grep \"$AFTER_TIME\" <output-dir>/after/LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log > after.txt"
echo "  diff before.txt after.txt"
```

## PowerShell Equivalents

### Build and Deploy (PowerShell)
```powershell
# Build NuGet package (from the dd-trace-dotnet repo root)
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo <output-dir> -Verbose

# Deploy (from your Azure Functions app directory)
Set-Location <path-to-your-azure-functions-app>
dotnet restore
func azure functionapp publish <app-name>
```

### Download Logs (PowerShell)
```powershell
$AppName = "<app-name>"
$ResourceGroup = "<resource-group>"
$Timestamp = Get-Date -Format "HHmmss"
$OutputDir = "$env:TEMP"  # or any preferred directory
$LogFile = "$OutputDir\logs-$Timestamp.zip"

# Download
az webapp log download --name $AppName --resource-group $ResourceGroup --log-file $LogFile

# Extract
Expand-Archive -Path $LogFile -DestinationPath "$OutputDir\LogFiles-$Timestamp" -Force

Write-Host "Logs extracted to: $OutputDir\LogFiles-$Timestamp\LogFiles\datadog\"
```
