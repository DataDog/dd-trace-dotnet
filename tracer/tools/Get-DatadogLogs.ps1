<#
.SYNOPSIS
Retrieves logs from the Datadog Logs API.

.DESCRIPTION
Queries the Datadog Logs API to retrieve logs matching a specific query.
Requires DD_API_KEY and DD_APPLICATION_KEY environment variables to be set.

.PARAMETER Query
The log query to search for (e.g., "service:my-service error")

.PARAMETER TimeRange
How far back to search for logs. Defaults to "1h" (1 hour).
Examples: "15m", "1h", "2h", "1d"

.PARAMETER Limit
Maximum number of log entries to return (default: 50, max: 1000)

.PARAMETER OutputFormat
Output format: "table" (default), "json", or "raw"

.EXAMPLE
.\Get-DatadogLogs.ps1 -Query "service:lucasp-premium-linux-isolated AspNetCoreDiagnosticObserver"

.EXAMPLE
.\Get-DatadogLogs.ps1 -Query "service:my-app error" -TimeRange "2h" -Limit 100

.EXAMPLE
.\Get-DatadogLogs.ps1 -Query "service:my-app" -OutputFormat json | ConvertFrom-Json | ConvertTo-Json -Depth 10
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Query,

    [Parameter(Mandatory = $false)]
    [string]$TimeRange = "1h",

    [Parameter(Mandatory = $false)]
    [int]$Limit = 50,

    [Parameter(Mandatory = $false)]
    [ValidateSet("table", "json", "raw")]
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
$uri = "https://api.datadoghq.com/api/v2/logs/events/search"
$headers = @{
    "DD-API-KEY"         = $apiKey
    "DD-APPLICATION-KEY" = $appKey
    "Content-Type"       = "application/json"
}

$body = @{
    filter = @{
        query = $Query
        from  = "now-$TimeRange"
        to    = "now"
    }
    sort   = "timestamp"
    page   = @{
        limit = $Limit
    }
} | ConvertTo-Json -Depth 10

Write-Verbose "Querying Datadog Logs API with query: $Query"
Write-Verbose "Time range: now-$TimeRange to now"
Write-Verbose "Limit: $Limit"

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body -ErrorAction Stop

    if ($null -eq $response.data -or $response.data.Count -eq 0) {
        Write-Warning "No logs found for query: $Query"
        Write-Warning "Make sure the query is correct and logs exist within the time range (now-$TimeRange to now)"
        exit 0
    }

    $logs = $response.data | ForEach-Object {
        $attrs = $_.attributes

        [PSCustomObject]@{
            Timestamp = $attrs.timestamp
            Status    = $attrs.status
            Service   = $attrs.service
            Message   = $attrs.message
            Host      = $attrs.host
            Tags      = $attrs.tags -join ", "
        }
    }

    switch ($OutputFormat) {
        "table" {
            Write-Host "`nFound $($logs.Count) log entries:" -ForegroundColor Green
            Write-Host "Query: $Query`n" -ForegroundColor Cyan

            $logs | Format-Table -Property @(
                @{Label = "Timestamp"; Expression = {
                    $dt = [DateTime]::Parse($_.Timestamp)
                    $dt.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }; Width = 23 }
                @{Label = "Status"; Expression = { $_.Status }; Width = 8 }
                @{Label = "Service"; Expression = { $_.Service }; Width = 30 }
                @{Label = "Host"; Expression = { $_.Host }; Width = 20 }
                @{Label = "Message"; Expression = {
                    if ($_.Message.Length -gt 100) {
                        $_.Message.Substring(0, 97) + "..."
                    } else {
                        $_.Message
                    }
                }; Width = 100 }
            ) -AutoSize -Wrap
        }
        "json" {
            $logs | ConvertTo-Json -Depth 10
        }
        "raw" {
            $logs | ForEach-Object {
                $dt = [DateTime]::Parse($_.Timestamp)
                $ts = $dt.ToString("yyyy-MM-dd HH:mm:ss.fff")
                Write-Host "$ts [$($_.Status)] $($_.Service) - $($_.Message)"
            }
        }
    }

} catch {
    Write-Error "Failed to query Datadog Logs API: $_"
    Write-Error $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        Write-Error "API Response: $($_.ErrorDetails.Message)"
    }
    exit 1
}
