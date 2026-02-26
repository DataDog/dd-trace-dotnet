#Requires -Version 5.1

<#
.SYNOPSIS
    Analyzes Azure DevOps build failures for dd-trace-dotnet CI pipeline.

.DESCRIPTION
    Fetches build details, timeline, and failure information from Azure DevOps.
    Can compare failures against another build, extract test names, and download
    task logs. Provides structured output for manual or automated analysis.

.PARAMETER BuildId
    Azure DevOps build ID to analyze.

.PARAMETER PullRequest
    GitHub PR number. Extracts build ID via 'gh pr checks'.

.PARAMETER IncludeLogs
    Download task logs for failed tasks.

.PARAMETER OutputPath
    Directory for saved JSON artifacts and logs. Defaults to temp directory.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1

    Auto-detects the PR for the current git branch and analyzes its build.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345

    Analyzes build 12345 and displays summary table.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -PullRequest 8172

    Analyzes the Azure DevOps build for PR #8172.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345 -IncludeLogs -OutputPath D:\temp\ci-logs

    Analyzes build and downloads failed task logs to specified directory.

.EXAMPLE
    .\Get-AzureDevOpsBuildAnalysis.ps1 -BuildId 12345 | ConvertTo-Json -Depth 10

    Outputs structured JSON for programmatic consumption.
#>

[CmdletBinding(DefaultParameterSetName = 'ByCurrentBranch')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'ByBuildId')]
    [int]$BuildId,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByPullRequest')]
    [int]$PullRequest,

    [switch]$IncludeLogs,

    [string]$OutputPath = [System.IO.Path]::GetTempPath()
)

$ErrorActionPreference = 'Stop'

#region Helper Functions

function Invoke-AzDevOpsApi {
    param(
        [string]$Area,
        [string]$Resource,
        [string]$RouteParameters = '',
        [hashtable]$QueryParameters = @{},
        [string]$SaveToFile = ''
    )

    # Build argument array to avoid command injection via Invoke-Expression
    $azArgs = @(
        'devops', 'invoke',
        '--area', $Area,
        '--resource', $Resource,
        '--org', 'https://dev.azure.com/datadoghq',
        '--api-version', '6.0',
        '--detect', 'false'
    )

    if ($RouteParameters) {
        $azArgs += '--route-parameters'
        # Split space-separated parameters into individual arguments
        $azArgs += $RouteParameters -split '\s+'
    }

    if ($QueryParameters.Count -gt 0) {
        $azArgs += '--query-parameters'
        foreach ($kvp in $QueryParameters.GetEnumerator()) {
            $azArgs += "$($kvp.Key)=$($kvp.Value)"
        }
    }

    $cmdDisplay = "az $($azArgs -join ' ')"
    Write-Verbose "Executing: $cmdDisplay"

    # Capture stderr separately so az CLI warnings don't corrupt the JSON output
    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = & az @azArgs 2>$stderrFile

        if ($LASTEXITCODE -ne 0) {
            $stderr = Get-Content -Path $stderrFile -Raw -ErrorAction SilentlyContinue
            $errorMsg = @"
Azure DevOps API call failed
  Command: $cmdDisplay
  Area: $Area
  Resource: $Resource
  Exit Code: $LASTEXITCODE
  Error: $stderr
"@
            throw $errorMsg
        }
    }
    finally {
        Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
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

    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $checks = & gh pr checks $PRNumber --json name,link 2>$stderrFile
        if ($LASTEXITCODE -ne 0) {
            $stderr = Get-Content -Path $stderrFile -Raw -ErrorAction SilentlyContinue
            throw "Failed to get PR checks: $stderr"
        }
    }
    finally {
        Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
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

    # Patterns ordered by specificity (most specific first)
    $patterns = @(
        # xUnit format with dot-separated names:
        #   [xUnit.net 00:00:20.31]     Namespace.Class.Method [FAIL]
        '\[xUnit\.net[^\]]*\]\s+([A-Za-z0-9_.+<>]+(?:\([^\)]*\))?)\s+\[FAIL\]',

        # xUnit format with comma-separated names (profiler tests):
        #   [xUnit.net 00:05:32.66]     Datadog, Profiler, IntegrationTests, WindowsOnly, NamedPipeTestcs, CheckProfiles(...) [FAIL]
        '\[xUnit\.net[^\]]*\]\s+([A-Za-z0-9_, .+<>]+(?:\([^\)]*\))?)\s+\[FAIL\]',

        # Generic [FAIL] marker
        '\[FAIL\]\s+([^\r\n]+)',

        # Generic "Failed" prefix
        'Failed\s+([^\r\n]+)',

        # Span count mismatch with test name
        'Expected\s+\d+\s+spans.*?(?:in|at)\s+([A-Za-z0-9_.]+)',

        # Snapshot verification with test name
        'Received file does not match.*?([A-Za-z0-9_.]+)\.(?:verified|received)\.txt',

        # Test host process crash (CLR fatal error, access violation, etc.)
        #   The active test run was aborted. Reason: Test host process crashed : Fatal error. Internal CLR error. (0x80131506)
        #   The active test run was aborted. Reason: Test host process crashed
        'The active test run was aborted\.\s+Reason:\s+(.+)',

        # Framework-specific test failure
        #   Error testing net10.0
        #   Error testing netcoreapp3.1
        'Error testing\s+(net\S+)'
    )

    # After a crash header, subsequent messages may be bare test names.
    # Track when we've seen a crash header to capture the following lines.
    $inCrashBlock = $false

    foreach ($message in $Messages) {
        # Check if this message is a crash header
        if ($message -match 'test running when the crash occurred') {
            $inCrashBlock = $true
            continue
        }

        # If we're in a crash block, try to capture bare fully-qualified test names
        #   Polly.Specs.Retry.RetryTResultSpecsAsync.Should_wait_asynchronously_for_async_onretry_delegate
        if ($inCrashBlock) {
            $trimmed = $message.Trim()
            if ($trimmed -match '^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*){2,}$') {
                [void]$testNames.Add($trimmed)
                continue
            } else {
                # Non-matching line ends the crash block
                $inCrashBlock = $false
            }
        }

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

function Extract-BuildErrors {
    param([string[]]$Messages)

    $buildErrors = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($message in $Messages) {
        # Compilation errors: error CS0103, error NU1510, error NETSDK1045, error SYSLIB0001, etc.
        #   File.cs(81,18): error CS0103: The name 'StringUtils' does not exist in the current context [project.csproj]
        # Normalize paths (both Windows D:\a\... and Unix /Users/runner/...) to just filename(line,col)
        if ($message -match '(?:^|[\\/])([^\\/]+?\(\d+,\d+\)):\s+error\s+([A-Z]+\d+):\s+(.+?)(?:\s+\[|$)') {
            $location = $matches[1]
            $code = $matches[2]
            $description = $matches[3].Trim()
            [void]$buildErrors.Add("$code $location - $description")
            continue
        }

        # Nuke target exceptions
        #   Target "CompileManagedUnitTests" has thrown an exception
        #   Target "RunIntegrationTests" has thrown an exception
        if ($message -match 'Target\s+"([^"]+)"\s+has thrown an exception') {
            [void]$buildErrors.Add("Target ""$($matches[1])"" threw an exception")
        }
    }

    return @($buildErrors)
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

function Get-FailureHierarchy {
    param(
        [object]$Timeline
    )

    # Build lookup: record id -> record
    $recordsById = @{}
    foreach ($record in $Timeline.records) {
        $recordsById[$record.id] = $record
    }

    # Azure DevOps timeline hierarchy: Stage -> Phase -> Job -> Task
    # Phase is an internal container not shown in the UI — we skip it in the output.
    # We walk up parentId chains to resolve: Task -> Job -> Stage

    # Helper: walk up parent chain to find ancestor of a given type
    function Find-Ancestor {
        param([string]$RecordId, [string]$AncestorType)
        $currentId = $RecordId
        $maxDepth = 5
        while ($currentId -and $maxDepth -gt 0) {
            $parent = $recordsById[$currentId]
            if (-not $parent) { return $null }
            if ($parent.type -eq $AncestorType) { return $parent.id }
            $currentId = $parent.parentId
            $maxDepth--
        }
        return $null
    }

    # Helper: format duration from start/finish times
    function Format-Duration {
        param([object]$Record)
        if ($Record.startTime -and $Record.finishTime) {
            $start = [datetime]::Parse($Record.startTime)
            $finish = [datetime]::Parse($Record.finishTime)
            $minutes = [math]::Round(($finish - $start).TotalMinutes, 1)
            return "$minutes min"
        }
        return $null
    }

    $stageMap = [ordered]@{}

    # Ensure-Stage: get or create stage entry in the map
    function Ensure-Stage {
        param([string]$StageId)
        if (-not $stageMap.Contains($StageId)) {
            $stageRecord = $recordsById[$StageId]
            $stageMap[$StageId] = [PSCustomObject]@{
                Name   = if ($stageRecord) { $stageRecord.name } else { '(unknown stage)' }
                Result = if ($stageRecord) { $stageRecord.result } else { 'unknown' }
                Jobs   = [ordered]@{}
            }
        }
        return $stageMap[$StageId]
    }

    # Ensure-Job: get or create job entry under a stage
    function Ensure-Job {
        param([object]$StageEntry, [string]$JobId)
        if (-not $StageEntry.Jobs.Contains($JobId)) {
            $jobRecord = $recordsById[$JobId]
            $StageEntry.Jobs[$JobId] = [PSCustomObject]@{
                Name     = if ($jobRecord) { $jobRecord.name } else { '(unknown job)' }
                Result   = if ($jobRecord) { $jobRecord.result } else { 'unknown' }
                Duration = if ($jobRecord) { Format-Duration $jobRecord } else { $null }
                Tasks    = @()
            }
        }
        return $StageEntry.Jobs[$JobId]
    }

    # 1) Seed with all failed/canceled stages
    $Timeline.records | Where-Object {
        $_.type -eq 'Stage' -and ($_.result -eq 'failed' -or $_.result -eq 'canceled')
    } | ForEach-Object {
        [void](Ensure-Stage $_.id)
    }

    # 2) Add all failed/canceled jobs under their stages
    $Timeline.records | Where-Object {
        $_.type -eq 'Job' -and ($_.result -eq 'failed' -or $_.result -eq 'canceled')
    } | ForEach-Object {
        $stageId = Find-Ancestor $_.parentId 'Stage'
        if ($stageId) {
            $stageEntry = Ensure-Stage $stageId
            [void](Ensure-Job $stageEntry $_.id)
        }
    }

    # 3) Add all failed tasks under their jobs (and stages)
    $Timeline.records | Where-Object {
        $_.type -eq 'Task' -and $_.result -eq 'failed'
    } | ForEach-Object {
        $jobId = Find-Ancestor $_.parentId 'Job'
        if (-not $jobId) { return }
        $stageId = Find-Ancestor $recordsById[$jobId].parentId 'Stage'
        if (-not $stageId) { return }

        $stageEntry = Ensure-Stage $stageId
        $jobEntry = Ensure-Job $stageEntry $jobId

        $jobEntry.Tasks += [PSCustomObject]@{
            Name   = $_.name
            Result = $_.result
        }
    }

    # 4) Convert ordered dictionaries to arrays for clean output
    $results = @()
    foreach ($stageEntry in $stageMap.Values) {
        $jobArray = @()
        foreach ($jobEntry in $stageEntry.Jobs.Values) {
            $jobArray += [PSCustomObject]@{
                Name     = $jobEntry.Name
                Result   = $jobEntry.Result
                Duration = $jobEntry.Duration
                Tasks    = $jobEntry.Tasks
            }
        }
        $results += [PSCustomObject]@{
            Name   = $stageEntry.Name
            Result = $stageEntry.Result
            Jobs   = $jobArray
        }
    }

    return $results
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

    if ($PSCmdlet.ParameterSetName -eq 'ByCurrentBranch' -or $PSCmdlet.ParameterSetName -eq 'ByPullRequest') {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
            throw "GitHub CLI (gh) not found. Install from https://cli.github.com"
        }

        if ($PSCmdlet.ParameterSetName -eq 'ByCurrentBranch') {
            Write-Verbose "No arguments provided. Detecting PR for current branch..."
            $stderrFile = [System.IO.Path]::GetTempFileName()
            try {
                $prOutput = & gh pr view --json number -q .number 2>$stderrFile
                if ($LASTEXITCODE -ne 0) {
                    throw "No PR found for current branch. Specify -PullRequest or -BuildId."
                }
            }
            finally {
                Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
            }
            $PullRequest = [int]$prOutput
            Write-Verbose "Detected PR #$PullRequest for current branch."
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
    $buildErrors = @(Extract-BuildErrors -Messages $errorMessages)

    # Build full hierarchy: Stage -> Job -> Task for failed/canceled items
    $failureHierarchy = @(Get-FailureHierarchy -Timeline $timeline)

    # Extract PR info from triggerInfo (available for PR-triggered builds)
    $prNumber = $null
    $prSourceBranch = $null
    $prUrl = $null
    if ($buildDetails.triggerInfo) {
        $prNumber = $buildDetails.triggerInfo.'pr.number'
        $prSourceBranch = $buildDetails.triggerInfo.'pr.sourceBranch'
        if ($prNumber) {
            $prUrl = "https://github.com/DataDog/dd-trace-dotnet/pull/$prNumber"
        }
    }

    # Build result object
    $result = [PSCustomObject]@{
        BuildId         = $buildDetails.id
        BuildNumber     = $buildDetails.buildNumber
        Status          = $buildDetails.status
        Result          = $buildDetails.result
        Branch          = if ($prSourceBranch) { $prSourceBranch } else { $buildDetails.sourceBranch }
        Commit          = $buildDetails.sourceVersion
        PrNumber        = $prNumber
        PrUrl           = $prUrl
        FinishTime      = $buildDetails.finishTime
        FailedTaskCount = $failedTasks.Count
        FailedTasks     = @($failedTasks | ForEach-Object { $_.name })
        FailedJobs      = @($failedJobs | ForEach-Object { $_.name })
        FailedStages    = @($failedStages | ForEach-Object { $_.name })
        FailureHierarchy     = $failureHierarchy
        CanceledJobs         = @($canceledJobs | ForEach-Object { $_.Name })
        TimedOutJobs         = @($timedOutJobs | ForEach-Object { "$($_.Name) ($($_.DurationMinutes) min)" })
        CollateralCanceled   = @($collateralCanceledJobs | ForEach-Object { $_.Name })
        FailedTests     = $failedTests
        BuildErrors     = $buildErrors
        ErrorMessages   = $errorMessages
        LogFiles        = @()
        ArtifactPath    = $OutputPath
        BuildUrl        = $buildDetails._links.web.href
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

    # Display summary to host
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
    if ($result.PrNumber) {
        Write-Host "  PR:           " -NoNewline; Write-Host "#$($result.PrNumber)" -ForegroundColor DarkGray -NoNewline; Write-Host " ($($result.PrUrl))" -ForegroundColor DarkGray
    }
    Write-Host "  URL:          " -NoNewline; Write-Host $result.BuildUrl -ForegroundColor DarkGray

    Write-Host "`nFailure Counts" -ForegroundColor Cyan
    Write-Host "  Failed Stages: " -NoNewline; Write-Host $result.FailedStages.Count -ForegroundColor Green
    Write-Host "  Failed Jobs:   " -NoNewline; Write-Host $result.FailedJobs.Count -ForegroundColor Green
    Write-Host "  Failed Tasks:  " -NoNewline; Write-Host $result.FailedTasks.Count -ForegroundColor Green
    Write-Host "  Failed Tests:  " -NoNewline; Write-Host $result.FailedTests.Count -ForegroundColor Green
    Write-Host "  Build Errors:  " -NoNewline; Write-Host $result.BuildErrors.Count -ForegroundColor Green
    Write-Host "  Timed Out:     " -NoNewline; Write-Host $result.TimedOutJobs.Count -ForegroundColor Yellow
    Write-Host "  Canceled:      " -NoNewline; Write-Host $result.CanceledJobs.Count -ForegroundColor DarkGray

    if ($result.FailureHierarchy.Count -gt 0) {
        Write-Host "`nFailure Hierarchy (Stage > Job > Task):" -ForegroundColor Red
        foreach ($stage in $result.FailureHierarchy) {
            $stageIcon = if ($stage.Result -eq 'failed') { 'X' } elseif ($stage.Result -eq 'canceled') { '!' } else { '?' }
            $stageColor = if ($stage.Result -eq 'failed') { 'Red' } else { 'Yellow' }
            Write-Host "  [$stageIcon] $($stage.Name)" -ForegroundColor $stageColor
            foreach ($job in $stage.Jobs) {
                $jobIcon = if ($job.Result -eq 'failed') { 'X' } elseif ($job.Result -eq 'canceled') { '!' } else { '?' }
                $jobColor = if ($job.Result -eq 'failed') { 'Red' } else { 'Yellow' }
                $jobSuffix = if ($job.Duration) { " ($($job.Duration))" } else { '' }
                Write-Host "      [$jobIcon] $($job.Name)$jobSuffix" -ForegroundColor $jobColor
                foreach ($task in $job.Tasks) {
                    Write-Host "          - $($task.Name)" -ForegroundColor DarkGray
                }
            }
        }
    }

    if ($result.BuildErrors.Count -gt 0) {
        Write-Host "`nBuild Errors:" -ForegroundColor Red
        $result.BuildErrors | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
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

    if ($result.LogFiles.Count -gt 0) {
        Write-Host "`nDownloaded Logs:" -ForegroundColor Cyan
        $result.LogFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor DarkGray }
    }

    Write-Host "`nArtifacts saved to: " -NoNewline; Write-Host $result.ArtifactPath -ForegroundColor DarkGray

    # Return object to pipeline
    return $result
}
catch {
    Write-Error "Build analysis failed: $_"
    exit 1
}
