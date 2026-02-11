<#
.SYNOPSIS
    Analyzes Azure DevOps build failures for dd-trace-dotnet CI pipeline.

.DESCRIPTION
    Fetches build details, timeline, and failure information from Azure DevOps.
    Can compare failures against master or another build, extract test names,
    and download task logs. Provides structured output for manual or automated analysis.

.PARAMETER BuildId
    Azure DevOps build ID to analyze.

.PARAMETER PullRequest
    GitHub PR number. Extracts build ID via 'gh pr checks'.

.PARAMETER CompareWithMaster
    Compare failed tests with most recent successful master build.

.PARAMETER CompareWithBuild
    Compare failed tests with specific baseline build ID.

.PARAMETER IncludeLogs
    Download task logs for failed tasks.

.PARAMETER OutputPath
    Directory for saved JSON artifacts and logs. Defaults to temp directory.

.PARAMETER OutputFormat
    Output format: "table" (human-readable) or "json" (structured data).

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345

    Analyzes build 12345 and displays summary table.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -PullRequest 8172

    Analyzes the Azure DevOps build for PR #8172.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345 -CompareWithMaster -Verbose

    Compares build 12345 failures with most recent successful master build.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345 -IncludeLogs -OutputPath D:\temp\ci-logs

    Analyzes build and downloads failed task logs to specified directory.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345 -OutputFormat json | ConvertFrom-Json

    Outputs structured JSON for programmatic consumption.
#>

[CmdletBinding(DefaultParameterSetName = 'ByBuildId')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildId')]
    [int]$BuildId,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequest')]
    [int]$PullRequest,

    [Parameter(ParameterSetName = 'ByBuildId')]
    [Parameter(ParameterSetName = 'ByPullRequest')]
    [switch]$CompareWithMaster,

    [Parameter(ParameterSetName = 'ByBuildId')]
    [Parameter(ParameterSetName = 'ByPullRequest')]
    [int]$CompareWithBuild,

    [Parameter(ParameterSetName = 'ByBuildId')]
    [Parameter(ParameterSetName = 'ByPullRequest')]
    [switch]$IncludeLogs,

    [Parameter(ParameterSetName = 'ByBuildId')]
    [Parameter(ParameterSetName = 'ByPullRequest')]
    [string]$OutputPath = [System.IO.Path]::GetTempPath(),

    [Parameter(ParameterSetName = 'ByBuildId')]
    [Parameter(ParameterSetName = 'ByPullRequest')]
    [ValidateSet('table', 'json')]
    [string]$OutputFormat = 'table'
)

$ErrorActionPreference = 'Stop'

# Verify PowerShell version (requires 5.1+ for ConvertTo-Json -Depth and -notin operator)
if ($PSVersionTable.PSVersion.Major -lt 5 -or
    ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -lt 1)) {
    Write-Error @"
This script requires PowerShell 5.1 or higher.
Current version: $($PSVersionTable.PSVersion)

Install PowerShell 7+ (recommended for cross-platform support):
- Windows: winget install Microsoft.PowerShell
- macOS: brew install powershell/tap/powershell
- Linux: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux

Or use PowerShell 5.1 (Windows only):
- Included with Windows 10/11 - run with: powershell.exe (not powershell.exe -Version 2)
"@
    exit 1
}

#region Helper Functions

function Invoke-AzDevOpsApi {
    param(
        [string]$Area,
        [string]$Resource,
        [string]$RouteParameters = '',
        [hashtable]$QueryParameters = @{},
        [string]$SaveToFile = ''
    )

    # Build command as string to avoid PowerShell splatting issues with --detect flag
    $cmd = "az devops invoke --area $Area --resource $Resource --org https://dev.azure.com/datadoghq --api-version 6.0 --detect false"

    if ($RouteParameters) {
        $cmd += " --route-parameters $RouteParameters"
    }

    if ($QueryParameters.Count -gt 0) {
        $queryString = ($QueryParameters.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '
        $cmd += " --query-parameters $queryString"
    }

    Write-Verbose "Executing: $cmd"
    $output = Invoke-Expression "$cmd 2>&1"

    if ($LASTEXITCODE -ne 0) {
        throw "Azure DevOps API call failed: $output"
    }

    $json = $output | ConvertFrom-Json

    if ($SaveToFile) {
        $json | ConvertTo-Json -Depth 100 | Set-Content -Path $SaveToFile -Encoding UTF8
        Write-Verbose "Saved to: $SaveToFile"
    }

    return $json
}

function Get-BuildIdFromPR {
    param([int]$PRNumber)

    Write-Verbose "Resolving build ID from PR #$PRNumber..."

    $checks = & gh pr checks $PRNumber --json name,link 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get PR checks: $checks"
    }

    $checksJson = $checks | ConvertFrom-Json
    # Find check with Azure DevOps URL (dev.azure.com)
    $azureCheck = $checksJson | Where-Object { $_.link -like '*dev.azure.com*' } | Select-Object -First 1

    if (-not $azureCheck) {
        throw "No Azure DevOps check found for PR #$PRNumber"
    }

    if ($azureCheck.link -match 'buildId=(\d+)') {
        $buildId = [int]$matches[1]
        Write-Verbose "Resolved build ID: $buildId from check: $($azureCheck.name)"
        return $buildId
    }

    throw "Could not extract build ID from URL: $($azureCheck.link)"
}

function Extract-FailedTests {
    param([string[]]$Messages)

    $testNames = [System.Collections.Generic.HashSet[string]]::new()

    $patterns = @(
        '\[FAIL\]\s+([^\r\n]+)',
        'Failed\s+([^\r\n]+)'
    )

    foreach ($message in $Messages) {
        foreach ($pattern in $patterns) {
            $matches = [regex]::Matches($message, $pattern)
            foreach ($match in $matches) {
                $testName = $match.Groups[1].Value.Trim()
                if ($testName) {
                    [void]$testNames.Add($testName)
                }
            }
        }
    }

    return @($testNames)
}

function Get-FailedRecords {
    param(
        [object]$Timeline,
        [string]$Type
    )

    return $Timeline.records | Where-Object {
        $_.result -eq 'failed' -and $_.type -eq $Type
    }
}

function Get-CanceledRecords {
    param(
        [object]$Timeline,
        [string]$Type
    )

    $canceledRecords = $Timeline.records | Where-Object {
        $_.result -eq 'canceled' -and $_.type -eq $Type
    }

    $results = @()
    foreach ($record in $canceledRecords) {
        $durationMinutes = 0
        $classification = 'unknown'

        if ($record.startTime -and $record.finishTime) {
            $startTime = [datetime]::Parse($record.startTime)
            $finishTime = [datetime]::Parse($record.finishTime)
            $durationMinutes = ($finishTime - $startTime).TotalMinutes
        }

        # Classify based on duration
        # >= 55 min: likely timeout (Azure DevOps has 60 min default)
        # < 5 min: likely collateral damage from parent cancellation
        # 5-55 min: unknown (could be manual cancellation or other)
        if ($durationMinutes -ge 55) {
            $classification = 'timeout'
        }
        elseif ($durationMinutes -lt 5) {
            $classification = 'collateral'
        }

        $results += [PSCustomObject]@{
            Name             = $record.name
            DurationMinutes  = [math]::Round($durationMinutes, 1)
            Classification   = $classification
        }
    }

    return $results
}

#endregion

try {
    # Prerequisite checks
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI (az) not found. Install from https://aka.ms/azure-cli"
    }

    if ($PSCmdlet.ParameterSetName -eq 'ByPullRequest') {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
            throw "GitHub CLI (gh) not found. Install from https://cli.github.com"
        }
        $BuildId = Get-BuildIdFromPR -PRNumber $PullRequest
    }

    Write-Host "Analyzing build $BuildId..." -ForegroundColor Cyan

    # Ensure output directory exists
    if (-not (Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }

    # Fetch build details
    $buildDetailsFile = Join-Path $OutputPath "build-$BuildId-details.json"
    $buildDetails = Invoke-AzDevOpsApi `
        -Area 'build' `
        -Resource 'builds' `
        -RouteParameters "project=dd-trace-dotnet buildId=$BuildId" `
        -SaveToFile $buildDetailsFile

    # Fetch timeline
    $timelineFile = Join-Path $OutputPath "build-$BuildId-timeline.json"
    $timeline = Invoke-AzDevOpsApi `
        -Area 'build' `
        -Resource 'timeline' `
        -RouteParameters "project=dd-trace-dotnet buildId=$BuildId" `
        -SaveToFile $timelineFile

    # Extract failures
    $failedTasks = @(Get-FailedRecords -Timeline $timeline -Type 'Task')
    $failedJobs = @(Get-FailedRecords -Timeline $timeline -Type 'Job')
    $failedStages = @(Get-FailedRecords -Timeline $timeline -Type 'Stage')

    $canceledJobs = @(Get-CanceledRecords -Timeline $timeline -Type 'Job')
    $timedOutJobs = @($canceledJobs | Where-Object { $_.Classification -eq 'timeout' })
    $collateralCanceledJobs = @($canceledJobs | Where-Object { $_.Classification -eq 'collateral' })

    $errorMessages = @($failedTasks | ForEach-Object {
        $_.issues | Where-Object { $_.type -eq 'error' } | ForEach-Object { $_.message }
    })

    $failedTests = @(Extract-FailedTests -Messages $errorMessages)

    # Build result object
    $result = [PSCustomObject]@{
        BuildId         = $buildDetails.id
        BuildNumber     = $buildDetails.buildNumber
        Status          = $buildDetails.status
        Result          = $buildDetails.result
        Branch          = $buildDetails.sourceBranch
        Commit          = $buildDetails.sourceVersion
        FinishTime      = $buildDetails.finishTime
        FailedTaskCount = $failedTasks.Count
        FailedTasks     = @($failedTasks | ForEach-Object { $_.name })
        FailedJobs      = @($failedJobs | ForEach-Object { $_.name })
        FailedStages    = @($failedStages | ForEach-Object { $_.name })
        CanceledJobs         = @($canceledJobs | ForEach-Object { $_.Name })
        TimedOutJobs         = @($timedOutJobs | ForEach-Object { "$($_.Name) ($($_.DurationMinutes) min)" })
        CollateralCanceled   = @($collateralCanceledJobs | ForEach-Object { $_.Name })
        FailedTests     = $failedTests
        ErrorMessages   = $errorMessages
        Comparison      = $null
        LogFiles        = @()
        ArtifactPath    = $OutputPath
        BuildUrl        = $buildDetails._links.web.href
    }

    # Comparison logic
    if ($CompareWithMaster -or $CompareWithBuild) {
        Write-Verbose "Performing comparison..."

        $baselineBuildId = $CompareWithBuild
        if ($CompareWithMaster) {
            Write-Verbose "Finding most recent successful master build..."
            $masterBuilds = Invoke-AzDevOpsApi `
                -Area 'build' `
                -Resource 'builds' `
                -RouteParameters 'project=dd-trace-dotnet' `
                -QueryParameters @{
                    branchName = 'refs/heads/master'
                    '$top'     = '10'
                }

            $successfulBuild = $masterBuilds.value | Where-Object { $_.result -eq 'succeeded' } | Select-Object -First 1
            if (-not $successfulBuild) {
                Write-Warning "No successful master build found in last 10 builds"
            }
            else {
                $baselineBuildId = $successfulBuild.id
            }
        }

        if ($baselineBuildId) {
            Write-Verbose "Comparing with baseline build $baselineBuildId..."

            $baselineTimelineFile = Join-Path $OutputPath "build-$baselineBuildId-timeline.json"
            $baselineTimeline = Invoke-AzDevOpsApi `
                -Area 'build' `
                -Resource 'timeline' `
                -RouteParameters "project=dd-trace-dotnet buildId=$baselineBuildId" `
                -SaveToFile $baselineTimelineFile

            $baselineFailedTasks = @(Get-FailedRecords -Timeline $baselineTimeline -Type 'Task')
            $baselineErrorMessages = @($baselineFailedTasks | ForEach-Object {
                $_.issues | Where-Object { $_.type -eq 'error' } | ForEach-Object { $_.message }
            })
            $baselineFailedTests = @(Extract-FailedTests -Messages $baselineErrorMessages)

            $newFailures = @($failedTests | Where-Object { $_ -notin $baselineFailedTests })
            $preExistingFailures = @($failedTests | Where-Object { $_ -in $baselineFailedTests })
            $fixedInPR = @($baselineFailedTests | Where-Object { $_ -notin $failedTests })

            $result.Comparison = [PSCustomObject]@{
                BaselineBuildId       = $baselineBuildId
                BaselineResult        = if ($CompareWithMaster) { 'succeeded' } else { $null }
                NewFailures           = $newFailures
                PreExistingFailures   = $preExistingFailures
                FixedInPR             = $fixedInPR
            }
        }
    }

    # Download logs
    if ($IncludeLogs -and $failedTasks.Count -gt 0) {
        Write-Verbose "Downloading logs for $($failedTasks.Count) failed tasks..."

        $logFiles = @()
        foreach ($task in $failedTasks) {
            if ($task.log -and $task.log.url) {
                $logFileName = "build-$BuildId-task-$($task.id)-$($task.name -replace '[^\w\-]', '_').log"
                $logFilePath = Join-Path $OutputPath $logFileName

                try {
                    Write-Verbose "Downloading log: $($task.name) -> $logFileName"
                    Invoke-RestMethod -Uri $task.log.url -OutFile $logFilePath -ErrorAction Stop
                    $logFiles += $logFilePath
                }
                catch {
                    Write-Warning "Failed to download log for task '$($task.name)': $_"
                }
            }
        }

        $result.LogFiles = $logFiles
    }

    # Output
    if ($OutputFormat -eq 'json') {
        $result | ConvertTo-Json -Depth 10
    }
    else {
        # Table format
        Write-Host "`nBuild Summary" -ForegroundColor Cyan
        Write-Host "  Build ID:     " -NoNewline; Write-Host $result.BuildId -ForegroundColor Yellow
        Write-Host "  Build Number: " -NoNewline; Write-Host $result.BuildNumber -ForegroundColor Yellow
        Write-Host "  Status:       " -NoNewline; Write-Host $result.Status -ForegroundColor Yellow
        Write-Host "  Result:       " -NoNewline
        if ($result.Result -eq 'failed') {
            Write-Host $result.Result -ForegroundColor Red
        }
        else {
            Write-Host $result.Result -ForegroundColor Green
        }
        Write-Host "  Branch:       " -NoNewline; Write-Host $result.Branch -ForegroundColor DarkGray
        Write-Host "  Commit:       " -NoNewline; Write-Host $result.Commit -ForegroundColor DarkGray
        Write-Host "  URL:          " -NoNewline; Write-Host $result.BuildUrl -ForegroundColor DarkGray

        Write-Host "`nFailure Counts" -ForegroundColor Cyan
        Write-Host "  Failed Stages: " -NoNewline; Write-Host $result.FailedStages.Count -ForegroundColor Green
        Write-Host "  Failed Jobs:   " -NoNewline; Write-Host $result.FailedJobs.Count -ForegroundColor Green
        Write-Host "  Failed Tasks:  " -NoNewline; Write-Host $result.FailedTasks.Count -ForegroundColor Green
        Write-Host "  Failed Tests:  " -NoNewline; Write-Host $result.FailedTests.Count -ForegroundColor Green
        Write-Host "  Timed Out:     " -NoNewline; Write-Host $result.TimedOutJobs.Count -ForegroundColor Yellow
        Write-Host "  Canceled:      " -NoNewline; Write-Host $result.CanceledJobs.Count -ForegroundColor DarkGray

        if ($result.FailedStages.Count -gt 0) {
            Write-Host "`nFailed Stages:" -ForegroundColor Red
            $result.FailedStages | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        if ($result.FailedJobs.Count -gt 0) {
            Write-Host "`nFailed Jobs:" -ForegroundColor Red
            $result.FailedJobs | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        if ($result.FailedTasks.Count -gt 0) {
            Write-Host "`nFailed Tasks:" -ForegroundColor Red
            $result.FailedTasks | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        if ($result.TimedOutJobs.Count -gt 0) {
            Write-Host "`nTimed Out Jobs (canceled after ~60 min):" -ForegroundColor Yellow
            $result.TimedOutJobs | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        if ($result.CollateralCanceled.Count -gt 0) {
            Write-Host "`nCollateral Cancellations (< 5 min, likely parent failure):" -ForegroundColor DarkGray
            $result.CollateralCanceled | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        if ($result.FailedTests.Count -gt 0) {
            Write-Host "`nFailed Tests:" -ForegroundColor Red
            $result.FailedTests | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        if ($result.Comparison) {
            Write-Host "`nComparison with Build $($result.Comparison.BaselineBuildId)" -ForegroundColor Cyan
            Write-Host "  New Failures:        " -NoNewline; Write-Host $result.Comparison.NewFailures.Count -ForegroundColor Red
            Write-Host "  Pre-existing:        " -NoNewline; Write-Host $result.Comparison.PreExistingFailures.Count -ForegroundColor Yellow
            Write-Host "  Fixed in this build: " -NoNewline; Write-Host $result.Comparison.FixedInPR.Count -ForegroundColor Green

            if ($result.Comparison.NewFailures.Count -gt 0) {
                Write-Host "`n  New Failures:" -ForegroundColor Red
                $result.Comparison.NewFailures | ForEach-Object { Write-Host "    - $_" -ForegroundColor DarkGray }
            }

            if ($result.Comparison.FixedInPR.Count -gt 0) {
                Write-Host "`n  Fixed:" -ForegroundColor Green
                $result.Comparison.FixedInPR | ForEach-Object { Write-Host "    - $_" -ForegroundColor DarkGray }
            }
        }

        if ($result.LogFiles.Count -gt 0) {
            Write-Host "`nDownloaded Logs:" -ForegroundColor Cyan
            $result.LogFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
        }

        Write-Host "`nArtifacts saved to: " -NoNewline; Write-Host $result.ArtifactPath -ForegroundColor DarkGray
    }

    # Return object to pipeline
    return $result
}
catch {
    Write-Error "Build analysis failed: $_"
    exit 1
}
