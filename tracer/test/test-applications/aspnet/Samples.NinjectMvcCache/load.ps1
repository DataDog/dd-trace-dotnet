param(
    [string] $BaseUrl = 'http://localhost:61914',
    [ValidateRange(1, 256)]
    [int] $Concurrency = 32,
    [ValidateRange(1, 100000)]
    [int] $Requests = 1000,
    [ValidateRange(1, 10000)]
    [int] $Iterations = 250
)

$ErrorActionPreference = 'Stop'
$runUrl = "$($BaseUrl.TrimEnd('/'))/Stress/Run?iterations=$Iterations"
$cacheUrl = "$($BaseUrl.TrimEnd('/'))/Stress/Cache"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$results = 1..$Requests | ForEach-Object -ThrottleLimit $Concurrency -Parallel {
    $requestStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Invoke-RestMethod -Method Get -Uri $using:runUrl | Out-Null
        [pscustomobject]@{ Success = $true; Milliseconds = $requestStopwatch.Elapsed.TotalMilliseconds }
    }
    catch {
        [pscustomobject]@{ Success = $false; Milliseconds = $requestStopwatch.Elapsed.TotalMilliseconds; Error = $_.Exception.Message }
    }
}

$stopwatch.Stop()
$ordered = @($results | Where-Object Success | ForEach-Object Milliseconds | Sort-Object)
function Get-Percentile([double[]] $Values, [double] $Percentile) {
    if ($Values.Count -eq 0) { return 0 }
    $index = [Math]::Min($Values.Count - 1, [Math]::Floor($Percentile * $Values.Count))
    return $Values[$index]
}

[pscustomobject]@{
    Requests = $Requests
    Successful = @($results | Where-Object Success).Count
    Failed = @($results | Where-Object { -not $_.Success }).Count
    Concurrency = $Concurrency
    Iterations = $Iterations
    WallSeconds = $stopwatch.Elapsed.TotalSeconds
    P50Milliseconds = Get-Percentile $ordered 0.50
    P95Milliseconds = Get-Percentile $ordered 0.95
    P99Milliseconds = Get-Percentile $ordered 0.99
}

Invoke-RestMethod -Method Get -Uri $cacheUrl | ConvertTo-Json -Depth 5
