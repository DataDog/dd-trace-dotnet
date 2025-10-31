<#
.SYNOPSIS
Retrieves all spans for a given trace ID from the Datadog API.

.DESCRIPTION
Queries the Datadog Spans API to retrieve all spans belonging to a specific trace ID.
Requires DD_API_KEY and DD_APPLICATION_KEY environment variables to be set.

.PARAMETER TraceId
The 128-bit trace ID to query (hex string, e.g., "690507fc00000000b882bcd2bdac6b9e")

.PARAMETER TimeRange
How far back to search for the trace. Defaults to "2h" (2 hours).
Examples: "15m", "1h", "2h", "1d"

.PARAMETER OutputFormat
Output format: "table" (default), "json", or "hierarchy"

.EXAMPLE
.\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e"

.EXAMPLE
.\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -OutputFormat hierarchy

.EXAMPLE
.\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -OutputFormat json | ConvertFrom-Json | ConvertTo-Json -Depth 10
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$TraceId,

    [Parameter(Mandatory = $false)]
    [string]$TimeRange = "2h",

    [Parameter(Mandatory = $false)]
    [ValidateSet("table", "json", "hierarchy")]
    [string]$OutputFormat = "table"
)

# Check for required environment variables
$apiKey = $env:DD_API_KEY
$appKey = $env:DD_APPLICATION_KEY

if ([string]::IsNullOrEmpty($apiKey)) {
    Write-Error "DD_API_KEY environment variable is not set. Please set it before running this script."
    exit 1
}

if ([string]::IsNullOrEmpty($appKey)) {
    Write-Error "DD_APPLICATION_KEY environment variable is not set. Please set it before running this script."
    exit 1
}

# Build the request
$uri = "https://api.datadoghq.com/api/v2/spans/events/search"
$headers = @{
    "DD-API-KEY"         = $apiKey
    "DD-APPLICATION-KEY" = $appKey
    "Content-Type"       = "application/json"
}

$body = @{
    data = @{
        attributes = @{
            filter = @{
                query = "trace_id:$TraceId"
                from  = "now-$TimeRange"
                to    = "now"
            }
            sort   = "start_timestamp"
            page   = @{
                limit = 100
            }
        }
        type       = "search_request"
    }
} | ConvertTo-Json -Depth 10

Write-Verbose "Querying Datadog API for trace ID: $TraceId"
Write-Verbose "Time range: now-$TimeRange to now"

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body -ErrorAction Stop

    if ($null -eq $response.data -or $response.data.Count -eq 0) {
        Write-Warning "No spans found for trace ID: $TraceId"
        Write-Warning "Make sure the trace ID is correct and the trace is within the time range (now-$TimeRange to now)"
        exit 0
    }

    $spans = $response.data | ForEach-Object {
        $attrs = $_.attributes

        # Convert duration (nanoseconds to milliseconds)
        $durationMs = if ($attrs.custom.duration) {
            [math]::Round([double]$attrs.custom.duration / 1000000.0, 2)
        } else {
            0
        }

        [PSCustomObject]@{
            SpanId        = $attrs.span_id
            ParentId      = $attrs.parent_id
            OperationName = $attrs.operation_name
            ResourceName  = $attrs.resource_name
            Status        = $attrs.status
            Duration      = $durationMs
            Process       = $attrs.custom.aas.function.process
            Service       = $attrs.service
        }
    }

    switch ($OutputFormat) {
        "table" {
            Write-Host "`nFound $($spans.Count) spans in trace:" -ForegroundColor Green
            Write-Host "Trace ID: $TraceId`n" -ForegroundColor Cyan

            $spans | Format-Table -Property @(
                @{Label = "Operation"; Expression = { $_.OperationName }; Width = 25 }
                @{Label = "Resource"; Expression = { $_.ResourceName }; Width = 30 }
                @{Label = "Span ID"; Expression = { $_.SpanId }; Width = 20 }
                @{Label = "Parent ID"; Expression = { $_.ParentId }; Width = 20 }
                @{Label = "Process"; Expression = { $_.Process }; Width = 8 }
                @{Label = "Duration (ms)"; Expression = { $_.Duration }; Width = 12 }
            ) -AutoSize
        }
        "json" {
            $spans | ConvertTo-Json -Depth 10
        }
        "hierarchy" {
            Write-Host "`nFound $($spans.Count) spans in trace:" -ForegroundColor Green
            Write-Host "Trace ID: $TraceId`n" -ForegroundColor Cyan

            # Build hierarchy
            $spanMap = @{}
            foreach ($span in $spans) {
                $spanMap[$span.SpanId] = $span
            }

            function Show-SpanHierarchy {
                param(
                    [PSCustomObject]$Span,
                    [int]$Indent = 0,
                    [hashtable]$SpanMap,
                    [PSCustomObject[]]$AllSpans
                )

                $prefix = "  " * $Indent
                $arrow = if ($Indent -gt 0) { "└─ " } else { "" }

                $processTag = if ($Span.Process) { " [$($Span.Process)]" } else { "" }
                Write-Host "$prefix$arrow$($Span.OperationName)$processTag" -ForegroundColor Cyan
                Write-Host "$prefix   Resource: $($Span.ResourceName)" -ForegroundColor Gray
                Write-Host "$prefix   Span ID: $($Span.SpanId), Parent: $($Span.ParentId)" -ForegroundColor DarkGray
                Write-Host "$prefix   Duration: $($Span.Duration) ms`n" -ForegroundColor DarkGray

                # Find children
                $children = $AllSpans | Where-Object { $_.ParentId -eq $Span.SpanId }
                foreach ($child in $children) {
                    Show-SpanHierarchy -Span $child -Indent ($Indent + 1) -SpanMap $SpanMap -AllSpans $AllSpans
                }
            }

            # Find root span(s) (parent_id = 0)
            $rootSpans = $spans | Where-Object { $_.ParentId -eq "0" }

            if ($rootSpans.Count -eq 0) {
                Write-Warning "No root span found (parent_id = 0). Showing all spans:"
                $spans | ForEach-Object {
                    Show-SpanHierarchy -Span $_ -Indent 0 -SpanMap $spanMap -AllSpans $spans
                }
            } else {
                foreach ($root in $rootSpans) {
                    Show-SpanHierarchy -Span $root -Indent 0 -SpanMap $spanMap -AllSpans $spans
                }
            }
        }
    }

} catch {
    Write-Error "Failed to query Datadog API: $_"
    Write-Error $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        Write-Error "API Response: $($_.ErrorDetails.Message)"
    }
    exit 1
}
