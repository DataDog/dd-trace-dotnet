#Requires -Version 5.1

<#
.SYNOPSIS
    Retries failed or canceled stages in an Azure DevOps build.

.DESCRIPTION
    Fetches the build timeline, identifies failed/canceled stages, and retries
    them via the Azure DevOps REST API (PATCH). Supports retrying all failed
    stages, specific stages by identifier, or interactive selection.

.PARAMETER BuildId
    Azure DevOps build ID.

.PARAMETER PullRequest
    GitHub PR number. Extracts build ID via 'gh pr checks'.

.PARAMETER All
    Retry all failed/canceled stages without prompting.

.PARAMETER Stage
    One or more stage identifiers to retry (e.g., 'integration_tests_linux').

.PARAMETER ForceRetryAllJobs
    Rerun all jobs in the stage, not just the failed ones.

.EXAMPLE
    .\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -All

    Retries all failed/canceled stages in build 197249.

.EXAMPLE
    .\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -Stage integration_tests_linux

    Retries only the integration_tests_linux stage.

.EXAMPLE
    .\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -All -WhatIf

    Shows which stages would be retried without actually retrying.

.EXAMPLE
    .\Retry-AzureDevOpsFailedStages.ps1 -BuildId 197249 -All -ForceRetryAllJobs

    Retries all failed stages, rerunning all jobs (not just failed ones).
#>

[CmdletBinding(DefaultParameterSetName = 'ByCurrentBranch', SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildId')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildIdAll')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildIdStage')]
    [int]$BuildId,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequest')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequestAll')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequestStage')]
    [int]$PullRequest,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildIdAll')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequestAll')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByCurrentBranchAll')]
    [switch]$All,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildIdStage')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequestStage')]
    [Parameter(Mandatory = $true, ParameterSetName = 'ByCurrentBranchStage')]
    [string[]]$Stage,

    [switch]$ForceRetryAllJobs
)

$ErrorActionPreference = 'Stop'

# Import shared module
$modulePath = Join-Path $PSScriptRoot 'AzureDevOpsHelpers.psm1'
Import-Module $modulePath -Force

# Determine the base parameter set name for Resolve-BuildId
$baseSetName = switch -Wildcard ($PSCmdlet.ParameterSetName) {
    'ByBuildId*'       { 'ByBuildId' }
    'ByPullRequest*'   { 'ByPullRequest' }
    'ByCurrentBranch*' { 'ByCurrentBranch' }
}

$BuildId = Resolve-BuildId -ParameterSetName $baseSetName -BuildId $BuildId -PullRequest $PullRequest

Write-Host "Fetching timeline for build $BuildId..." -ForegroundColor Cyan

# Fetch timeline
$timeline = Invoke-AzDevOpsApi `
    -Area 'build' `
    -Resource 'timeline' `
    -RouteParameters "project=dd-trace-dotnet buildId=$BuildId"

# Find failed/canceled stages
$failedStages = @($timeline.records | Where-Object {
    $_.type -eq 'Stage' -and ($_.result -eq 'failed' -or $_.result -eq 'canceled')
})

if ($failedStages.Count -eq 0) {
    Write-Host "No failed or canceled stages found in build $BuildId." -ForegroundColor Green
    return
}

Write-Host "`nFailed/canceled stages ($($failedStages.Count)):" -ForegroundColor Yellow
foreach ($s in $failedStages) {
    $icon = if ($s.result -eq 'failed') { 'X' } else { '!' }
    $color = if ($s.result -eq 'failed') { 'Red' } else { 'Yellow' }
    Write-Host "  [$icon] $($s.identifier) ($($s.result))" -ForegroundColor $color
}

# Select stages to retry
$stagesToRetry = @()

if ($All) {
    $stagesToRetry = $failedStages
}
elseif ($Stage) {
    foreach ($name in $Stage) {
        $match = $failedStages | Where-Object { $_.identifier -eq $name }
        if (-not $match) {
            $available = ($failedStages | ForEach-Object { $_.identifier }) -join ', '
            Write-Warning "Stage '$name' not found among failed/canceled stages. Available: $available"
        }
        else {
            $stagesToRetry += $match
        }
    }

    if ($stagesToRetry.Count -eq 0) {
        Write-Error "No matching stages found to retry."
        return
    }
}
else {
    # Interactive selection
    if (-not [Environment]::UserInteractive) {
        Write-Error "Non-interactive session: specify -All or -Stage to select stages."
        return
    }

    Write-Host "`nSelect stages to retry:" -ForegroundColor Cyan
    for ($i = 0; $i -lt $failedStages.Count; $i++) {
        Write-Host "  $($i + 1). $($failedStages[$i].identifier) ($($failedStages[$i].result))"
    }
    Write-Host "  A. All"

    $selection = Read-Host "`nEnter selection (comma-separated numbers, or A for all)"

    if ($selection -eq 'A' -or $selection -eq 'a') {
        $stagesToRetry = $failedStages
    }
    else {
        $indices = $selection -split ',' | ForEach-Object {
            $trimmed = $_.Trim()
            if ($trimmed -match '^\d+$') {
                [int]$trimmed - 1
            }
        }

        foreach ($idx in $indices) {
            if ($idx -ge 0 -and $idx -lt $failedStages.Count) {
                $stagesToRetry += $failedStages[$idx]
            }
            else {
                Write-Warning "Invalid selection: $($idx + 1)"
            }
        }
    }

    if ($stagesToRetry.Count -eq 0) {
        Write-Host "No stages selected." -ForegroundColor Yellow
        return
    }
}

# Retry each stage
Write-Host "`nRetrying $($stagesToRetry.Count) stage(s)..." -ForegroundColor Cyan

$results = @()

foreach ($stageRecord in $stagesToRetry) {
    $identifier = $stageRecord.identifier
    $displayName = $stageRecord.name

    if (-not $PSCmdlet.ShouldProcess("Stage '$displayName' ($identifier) in build $BuildId", 'Retry')) {
        $results += [PSCustomObject]@{
            BuildId    = $BuildId
            StageName  = $displayName
            Identifier = $identifier
            Result     = 'Skipped'
            Error      = 'WhatIf or user declined'
        }
        continue
    }

    try {
        $body = @{
            forceRetryAllJobs = [bool]$ForceRetryAllJobs
            state             = 'retry'
        }

        Invoke-AzDevOpsApi `
            -Area 'build' `
            -Resource 'stages' `
            -RouteParameters "project=dd-trace-dotnet buildId=$BuildId stageRefName=$identifier" `
            -HttpMethod 'PATCH' `
            -Body $body `
            -ApiVersion '7.1' | Out-Null

        Write-Host "  Retried: $displayName ($identifier)" -ForegroundColor Green

        $results += [PSCustomObject]@{
            BuildId    = $BuildId
            StageName  = $displayName
            Identifier = $identifier
            Result     = 'RetryRequested'
            Error      = $null
        }
    }
    catch {
        Write-Warning "  Failed to retry '$displayName' ($identifier): $_"

        $results += [PSCustomObject]@{
            BuildId    = $BuildId
            StageName  = $displayName
            Identifier = $identifier
            Result     = 'Failed'
            Error      = $_.ToString()
        }
    }
}

# Summary
$retried = @($results | Where-Object { $_.Result -eq 'RetryRequested' })
$failed = @($results | Where-Object { $_.Result -eq 'Failed' })
$skipped = @($results | Where-Object { $_.Result -eq 'Skipped' })

Write-Host "`nResults: $($retried.Count) retried, $($failed.Count) failed, $($skipped.Count) skipped" -ForegroundColor Cyan

if ($retried.Count -gt 0) {
    $buildUrl = "https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$BuildId"
    Write-Host "Build: $buildUrl" -ForegroundColor DarkGray
}

# Return results to pipeline
return $results
