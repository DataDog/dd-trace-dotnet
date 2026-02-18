#Requires -Version 5.1

<#
.SYNOPSIS
    Downloads and analyzes Azure Function logs for Datadog tracer diagnostics.

.DESCRIPTION
    Downloads application logs from an Azure Function App, extracts the archive,
    identifies host and worker log files, and performs optional analysis:
    - Tracer version detection
    - Span count at a specific timestamp
    - Trace parenting validation (checking for root span duplication)

.PARAMETER AppName
    The Azure Function App name.

.PARAMETER ResourceGroup
    The Azure resource group containing the Function App.

.PARAMETER OutputPath
    Directory where logs will be saved. Defaults to current directory.

.PARAMETER ExecutionTimestamp
    UTC timestamp in format "yyyy-MM-dd HH:mm:ss" to filter log analysis.
    Required for -ShowSpans and -CheckParenting.

.PARAMETER ShowVersion
    Display the Datadog tracer version from worker logs.

.PARAMETER ShowSpans
    Count spans created at the specified ExecutionTimestamp, split by host/worker.

.PARAMETER CheckParenting
    Analyze trace parenting: count root spans per process and unique trace IDs.

.PARAMETER All
    Enable all analysis options (ShowVersion, ShowSpans, CheckParenting).

.EXAMPLE
    .\Get-AzureFunctionLogs.ps1 -AppName "lucasp-premium-linux-isolated-aspnet" `
        -ResourceGroup "lucas.pimentel" `
        -ShowVersion

.EXAMPLE
    # Full analysis after deployment
    $deploy = .\Deploy-AzureFunction.ps1 -AppName "lucasp-premium-linux-isolated-aspnet" `
        -ResourceGroup "lucas.pimentel" `
        -SampleAppPath "D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore"

    .\Get-AzureFunctionLogs.ps1 -AppName $deploy.AppName `
        -ResourceGroup "lucas.pimentel" `
        -ExecutionTimestamp $deploy.ExecutionTimestamp `
        -All

.OUTPUTS
    PSCustomObject with properties:
    - LogZipPath: Path to downloaded zip file
    - ExtractDir: Directory where logs were extracted
    - DatadogLogDir: Directory containing Datadog tracer logs
    - TracerVersion: Detected tracer version (if -ShowVersion or -All)
    - SpanCount: Hashtable with HostSpans, WorkerSpans, TotalSpans (if -ShowSpans or -All)
    - ParentingAnalysis: Hashtable with trace parenting metrics (if -CheckParenting or -All)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AppName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [string]$OutputPath = ".",

    [string]$ExecutionTimestamp,

    [switch]$ShowVersion,

    [switch]$ShowSpans,

    [switch]$CheckParenting,

    [switch]$All
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Apply -All switch
if ($All) {
    $ShowVersion = $true
    $ShowSpans = $true
    $CheckParenting = $true
}

# Validate prerequisites
Write-Verbose "Verifying prerequisites..."

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) not found. Install from: https://docs.microsoft.com/cli/azure/install-azure-cli"
}

if (-not (Test-Path $OutputPath)) {
    throw "Output path does not exist: $OutputPath"
}

# Warn if timestamp-dependent analysis requested without timestamp
if (($ShowSpans -or $CheckParenting) -and -not $ExecutionTimestamp) {
    Write-Warning "ExecutionTimestamp not provided. Span count and parenting analysis will not be filtered by time."
}

# Resolve absolute output path
$OutputPath = (Resolve-Path $OutputPath).Path

# Download logs
$timestamp = Get-Date -Format "HHmmss"
$zipPath = Join-Path $OutputPath "logs-$timestamp.zip"

Write-Host "Downloading logs from $AppName..." -ForegroundColor Cyan
az webapp log download `
    --name $AppName `
    --resource-group $ResourceGroup `
    --log-file $zipPath

if ($LASTEXITCODE -ne 0) {
    throw "Failed to download logs (exit code $LASTEXITCODE)"
}

Write-Host "Logs downloaded to: $zipPath" -ForegroundColor Green

# Extract logs
$extractDir = Join-Path $OutputPath "logs-$timestamp"
Write-Host "Extracting logs to: $extractDir" -ForegroundColor Cyan
Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

# Locate Datadog logs directory
$datadogLogDir = Join-Path $extractDir "LogFiles\datadog"
if (-not (Test-Path $datadogLogDir)) {
    Write-Warning "Datadog logs directory not found at: $datadogLogDir"
    return [PSCustomObject]@{
        LogZipPath = $zipPath
        ExtractDir = $extractDir
        DatadogLogDir = $null
    }
}

Write-Verbose "Datadog logs found at: $datadogLogDir"

# Identify host and worker log files
$hostLogPattern = "dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log"
$workerLogPattern = "dotnet-tracer-managed-dotnet-*.log"

$hostLogs = @(Get-ChildItem -Path $datadogLogDir -Filter $hostLogPattern)
$workerLogs = @(Get-ChildItem -Path $datadogLogDir -Filter $workerLogPattern)

Write-Host "`nLog Files Found:" -ForegroundColor Cyan
Write-Host "  Host logs:   $($hostLogs.Count) files"
Write-Host "  Worker logs: $($workerLogs.Count) files"

# Initialize result object
$result = [PSCustomObject]@{
    LogZipPath = $zipPath
    ExtractDir = $extractDir
    DatadogLogDir = $datadogLogDir
    TracerVersion = $null
    SpanCount = $null
    ParentingAnalysis = $null
}

# Analysis: Tracer version
if ($ShowVersion) {
    Write-Host "`nDetecting tracer version..." -ForegroundColor Cyan

    if (-not $workerLogs -or $workerLogs.Count -eq 0) {
        Write-Warning "No worker logs found to extract version"
    } else {
        # Search for "Assembly metadata" in worker logs (most recent file first)
        $versionLine = $null
        foreach ($logFile in ($workerLogs | Sort-Object LastWriteTime -Descending)) {
            $versionLine = Get-Content $logFile.FullName | Select-String "Assembly metadata" | Select-Object -Last 1
            if ($versionLine) {
                break
            }
        }

        if ($versionLine) {
            # Extract version from log line formats:
            #   TracerVersion: "3.38.0.0"    (structured logging format)
            #   Version=3.38.0.0             (assembly metadata format)
            if ($versionLine -match 'TracerVersion:\s*"([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)"') {
                $result.TracerVersion = $matches[1]
                Write-Host "  Tracer version: $($result.TracerVersion)" -ForegroundColor Green
            } elseif ($versionLine -match "Version=([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)") {
                $result.TracerVersion = $matches[1]
                Write-Host "  Tracer version: $($result.TracerVersion)" -ForegroundColor Green
            } else {
                Write-Warning "Could not parse version from: $versionLine"
            }
        } else {
            Write-Warning "Assembly metadata line not found in worker logs"
        }
    }
}

# Analysis: Span count at timestamp
if ($ShowSpans) {
    Write-Host "`nCounting spans..." -ForegroundColor Cyan

    $hostSpanCount = 0
    $workerSpanCount = 0

    # Build time filter pattern (minute granularity: "yyyy-MM-dd HH:mm:")
    $timeFilter = $null
    if ($ExecutionTimestamp) {
        $timeFilter = $ExecutionTimestamp.Substring(0, 16)  # "yyyy-MM-dd HH:mm"
        Write-Verbose "Filtering spans to timestamp: $timeFilter"
    }

    # Count spans in host logs (excluding noisy infrastructure spans)
    foreach ($logFile in $hostLogs) {
        $lines = Get-Content $logFile.FullName
        if ($timeFilter) {
            $lines = $lines | Where-Object { $_ -match [regex]::Escape($timeFilter) }
        }
        $spanLines = @($lines | Select-String "span_id:" |
            Where-Object { $_ -notmatch "Operation: command_execution" } |
            Where-Object { $_ -notmatch "Resource: GET /admin/" } |
            Where-Object { $_ -notmatch "Resource: GET /robots.*\.txt" })
        $hostSpanCount += $spanLines.Count
    }

    # Count spans in worker logs
    foreach ($logFile in $workerLogs) {
        $lines = Get-Content $logFile.FullName
        if ($timeFilter) {
            $lines = $lines | Where-Object { $_ -match [regex]::Escape($timeFilter) }
        }
        $spanLines = @($lines | Select-String "span_id:")
        $workerSpanCount += $spanLines.Count
    }

    $result.SpanCount = @{
        HostSpans = $hostSpanCount
        WorkerSpans = $workerSpanCount
        TotalSpans = $hostSpanCount + $workerSpanCount
    }

    Write-Host "  Host spans:   $hostSpanCount"
    Write-Host "  Worker spans: $workerSpanCount"
    Write-Host "  Total spans:  $($result.SpanCount.TotalSpans)" -ForegroundColor Green
}

# Analysis: Trace parenting
if ($CheckParenting) {
    Write-Host "`nAnalyzing trace parenting..." -ForegroundColor Cyan

    $hostRootSpans = 0
    $workerRootSpans = 0
    $traceIds = @{}

    # Build time filter
    $timeFilter = $null
    if ($ExecutionTimestamp) {
        $timeFilter = $ExecutionTimestamp.Substring(0, 16)
        Write-Verbose "Filtering parenting analysis to timestamp: $timeFilter"
    }

    # Analyze host logs (excluding noisy infrastructure spans)
    foreach ($logFile in $hostLogs) {
        $lines = Get-Content $logFile.FullName
        if ($timeFilter) {
            $lines = $lines | Where-Object { $_ -match [regex]::Escape($timeFilter) }
        }

        foreach ($line in $lines) {
            # Skip noisy infrastructure spans in host process
            if ($line -match "Operation: command_execution") { continue }
            if ($line -match "Resource: GET /admin/") { continue }
            if ($line -match "Resource: GET /robots.*\.txt") { continue }

            # Look for spans with p_id: null (root spans)
            if ($line -match "p_id:\s*null") {
                $hostRootSpans++

                # Extract trace_id if present
                if ($line -match "trace_id:\s*(\d+)") {
                    $traceId = $matches[1]
                    if (-not $traceIds.ContainsKey($traceId)) {
                        $traceIds[$traceId] = @{ Host = 0; Worker = 0 }
                    }
                    $traceIds[$traceId].Host++
                }
            }
        }
    }

    # Analyze worker logs
    foreach ($logFile in $workerLogs) {
        $lines = Get-Content $logFile.FullName
        if ($timeFilter) {
            $lines = $lines | Where-Object { $_ -match [regex]::Escape($timeFilter) }
        }

        foreach ($line in $lines) {
            if ($line -match "p_id:\s*null") {
                $workerRootSpans++

                if ($line -match "trace_id:\s*(\d+)") {
                    $traceId = $matches[1]
                    if (-not $traceIds.ContainsKey($traceId)) {
                        $traceIds[$traceId] = @{ Host = 0; Worker = 0 }
                    }
                    $traceIds[$traceId].Worker++
                }
            }
        }
    }

    $result.ParentingAnalysis = @{
        HostRootSpans = $hostRootSpans
        WorkerRootSpans = $workerRootSpans
        TotalRootSpans = $hostRootSpans + $workerRootSpans
        UniqueTraceIds = $traceIds.Count
    }

    Write-Host "  Host root spans:   $hostRootSpans"
    Write-Host "  Worker root spans: $workerRootSpans"
    Write-Host "  Total root spans:  $($result.ParentingAnalysis.TotalRootSpans)"
    Write-Host "  Unique trace IDs:  $($result.ParentingAnalysis.UniqueTraceIds)"

    # Warning if multiple root spans detected
    if ($result.ParentingAnalysis.TotalRootSpans -gt $result.ParentingAnalysis.UniqueTraceIds) {
        Write-Warning "Multiple root spans detected for the same trace! This indicates a parenting issue."
    } else {
        Write-Host "  Parenting looks correct (1 root span per trace)" -ForegroundColor Green
    }
}

Write-Host "`nLogs extracted to: $extractDir" -ForegroundColor Cyan
return $result
