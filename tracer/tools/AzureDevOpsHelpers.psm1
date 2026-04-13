#Requires -Version 5.1

<#
.SYNOPSIS
    Shared helper functions for Azure DevOps CI scripts.

.DESCRIPTION
    Provides common functions for interacting with the Azure DevOps REST API.
    Uses the Azure CLI when available, with direct HTTP fallback for GET requests
    (the dd-trace-dotnet project is public). Mutating operations (PATCH/POST/PUT)
    require the Azure CLI. Used by Get-AzureDevOpsBuildAnalysis.ps1 and
    Retry-AzureDevOpsFailedStages.ps1.
#>

# Organization and project constants
$script:AzDevOpsOrg = 'https://dev.azure.com/datadoghq'
$script:AzDevOpsProject = 'dd-trace-dotnet'

function Invoke-AzDevOpsApi {
    <#
    .SYNOPSIS
        Invokes an Azure DevOps REST API endpoint.

    .DESCRIPTION
        Uses 'az devops invoke' when the Azure CLI is available. For GET requests,
        falls back to direct HTTP via Invoke-RestMethod (no auth required for public
        projects). Mutating requests (PATCH/POST/PUT) require the Azure CLI.

    .PARAMETER Area
        The API area (e.g., 'build').

    .PARAMETER Resource
        The API resource (e.g., 'builds', 'timeline', 'stages').

    .PARAMETER RouteParameters
        Space-separated route parameters (e.g., 'project=dd-trace-dotnet buildId=12345').

    .PARAMETER QueryParameters
        Hashtable of query parameters.

    .PARAMETER HttpMethod
        HTTP method (default: 'GET'). Use 'PATCH' for mutations.

    .PARAMETER Body
        Hashtable to serialize as JSON request body. Used with PATCH/POST/PUT.

    .PARAMETER ApiVersion
        Azure DevOps API version (default: '6.0').

    .PARAMETER SaveToFile
        Optional file path to save the JSON response.
    #>
    param(
        [string]$Area,
        [string]$Resource,
        [string]$RouteParameters = '',
        [hashtable]$QueryParameters = @{},
        [string]$HttpMethod = 'GET',
        [hashtable]$Body = $null,
        [string]$ApiVersion = '6.0',
        [string]$SaveToFile = ''
    )

    $hasAz = [bool](Get-Command az -ErrorAction SilentlyContinue)
    $isGet = $HttpMethod -eq 'GET'

    # Mutating requests require the az CLI (authentication needed)
    if (-not $isGet -and -not $hasAz) {
        throw "Azure CLI (az) is required for $HttpMethod requests. Install: https://aka.ms/azure-cli"
    }

    $json = $null
    $useHttp = $false

    # Try az CLI first if available
    if ($hasAz) {
        try {
            $json = Invoke-AzDevOpsApiViaCli -Area $Area -Resource $Resource `
                -RouteParameters $RouteParameters -QueryParameters $QueryParameters `
                -HttpMethod $HttpMethod -Body $Body -ApiVersion $ApiVersion
        }
        catch {
            if (-not $isGet) {
                # Mutating requests have no fallback — rethrow
                throw
            }
            Write-Warning "Azure CLI call failed, falling back to direct HTTP: $($_.Exception.Message)"
            $useHttp = $true
        }
    }
    else {
        $useHttp = $true
    }

    # Fall back to direct HTTP for GET requests when CLI is missing or failed
    if ($useHttp) {
        Write-Verbose "Using direct HTTP for Azure DevOps API GET request"
        $json = Invoke-AzDevOpsApiViaHttp -Area $Area -Resource $Resource `
            -RouteParameters $RouteParameters -QueryParameters $QueryParameters `
            -ApiVersion $ApiVersion
    }

    if ($SaveToFile -and $null -ne $json) {
        $json | ConvertTo-Json -Depth 100 | Set-Content -Path $SaveToFile -Encoding UTF8
        Write-Verbose "Saved to: $SaveToFile"
    }

    return $json
}

function Invoke-AzDevOpsApiViaCli {
    <#
    .SYNOPSIS
        Invokes an Azure DevOps REST API endpoint via 'az devops invoke'.
    #>
    param(
        [string]$Area,
        [string]$Resource,
        [string]$RouteParameters = '',
        [hashtable]$QueryParameters = @{},
        [string]$HttpMethod = 'GET',
        [hashtable]$Body = $null,
        [string]$ApiVersion = '6.0'
    )

    $azArgs = @(
        'devops', 'invoke',
        '--area', $Area,
        '--resource', $Resource,
        '--org', $script:AzDevOpsOrg,
        '--api-version', $ApiVersion,
        '--detect', 'false'
    )

    if ($HttpMethod -ne 'GET') {
        $azArgs += '--http-method'
        $azArgs += $HttpMethod
    }

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

    $inFile = $null
    if ($Body) {
        $inFile = [System.IO.Path]::GetTempFileName()
        $Body | ConvertTo-Json -Depth 10 | Set-Content -Path $inFile -Encoding UTF8
        $azArgs += '--in-file'
        $azArgs += $inFile
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
  Tip: If this is an authentication or permissions error, check your active subscription with 'az account show' and switch if needed with 'az account set --subscription <name>'
"@
            throw $errorMsg
        }
    }
    finally {
        Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
        if ($inFile) {
            Remove-Item -Path $inFile -Force -ErrorAction SilentlyContinue
        }
    }

    # PATCH/PUT may return 204 No Content (empty response)
    if (-not $output -or ($output -is [string] -and [string]::IsNullOrWhiteSpace($output))) {
        return $null
    }

    return $output | ConvertFrom-Json
}

function Invoke-AzDevOpsApiViaHttp {
    <#
    .SYNOPSIS
        Invokes an Azure DevOps REST API GET endpoint via direct HTTP.

    .DESCRIPTION
        Builds the REST URL from area/resource/route parameters and calls
        Invoke-RestMethod. No authentication is needed for GET requests
        against public Azure DevOps projects.
    #>
    param(
        [string]$Area,
        [string]$Resource,
        [string]$RouteParameters = '',
        [hashtable]$QueryParameters = @{},
        [string]$ApiVersion = '6.0'
    )

    # Parse route parameters into a hashtable
    $routeParams = @{}
    if ($RouteParameters) {
        foreach ($pair in ($RouteParameters -split '\s+')) {
            $parts = $pair -split '=', 2
            if ($parts.Count -eq 2) {
                $routeParams[$parts[0]] = $parts[1]
            }
        }
    }

    $project = if ($routeParams.ContainsKey('project')) { $routeParams['project'] } else { $script:AzDevOpsProject }

    # Build URL path based on area/resource/route parameters
    # Pattern: {org}/{project}/_apis/{area}/{resource}/{resourceId}[/{subResource}/{subResourceId}]
    $url = "$script:AzDevOpsOrg/$project/_apis/$Area"

    # Map well-known route parameter patterns to URL segments
    switch ($Area) {
        'build' {
            switch ($Resource) {
                'builds' {
                    if ($routeParams.ContainsKey('buildId')) {
                        $url += "/builds/$($routeParams['buildId'])"
                    } else {
                        $url += '/builds'
                    }
                }
                'timeline' {
                    $url += "/builds/$($routeParams['buildId'])/timeline"
                }
                'stages' {
                    $url += "/builds/$($routeParams['buildId'])/stages"
                    if ($routeParams.ContainsKey('stageRefName')) {
                        $url += "/$($routeParams['stageRefName'])"
                    }
                }
                default {
                    $url += "/$Resource"
                }
            }
        }
        default {
            $url += "/$Resource"
        }
    }

    # Add query parameters
    $queryParts = @("api-version=$([Uri]::EscapeDataString($ApiVersion))")
    foreach ($kvp in $QueryParameters.GetEnumerator()) {
        $queryParts += "$([Uri]::EscapeDataString($kvp.Key))=$([Uri]::EscapeDataString($kvp.Value))"
    }
    $url += "?$($queryParts -join '&')"

    Write-Verbose "HTTP GET: $url"

    try {
        return Invoke-RestMethod -Uri $url -Method Get -ContentType 'application/json'
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        throw @"
Azure DevOps HTTP request failed
  URL: $url
  Status: $statusCode
  Error: $($_.Exception.Message)
  Tip: If this is a 401/403 error, the project may require authentication. Install the Azure CLI: https://aka.ms/azure-cli
"@
    }
}

function Get-BuildIdFromPR {
    <#
    .SYNOPSIS
        Resolves an Azure DevOps build ID from a GitHub PR number.

    .DESCRIPTION
        Uses 'gh pr checks' when the GitHub CLI is available. Falls back to the
        GitHub REST API (get PR head SHA, then commit statuses) when gh is not
        installed. No authentication is needed for public repositories.

    .PARAMETER PRNumber
        The GitHub pull request number.
    #>
    param([int]$PRNumber)

    Write-Verbose "Resolving build ID from PR #$PRNumber..."

    $hasGh = [bool](Get-Command gh -ErrorAction SilentlyContinue)

    if ($hasGh) {
        try {
            return Get-BuildIdFromPRViaGh -PRNumber $PRNumber
        }
        catch {
            Write-Warning "GitHub CLI call failed, falling back to direct HTTP: $($_.Exception.Message)"
        }
    }
    else {
        Write-Verbose "GitHub CLI not found, using direct HTTP fallback"
    }

    return Get-BuildIdFromPRViaHttp -PRNumber $PRNumber
}

function Get-BuildIdFromPRViaGh {
    <#
    .SYNOPSIS
        Resolves build ID from PR checks using the GitHub CLI.
    #>
    param([int]$PRNumber)

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

function Get-BuildIdFromPRViaHttp {
    <#
    .SYNOPSIS
        Resolves build ID from PR commit statuses using the GitHub REST API.

    .DESCRIPTION
        Azure DevOps builds appear as commit statuses (not check runs) on GitHub.
        This function gets the PR's head SHA, then queries commit statuses to find
        the Azure DevOps build URL. No authentication required for public repos
        (rate-limited).
    #>
    param([int]$PRNumber)

    $repo = 'DataDog/dd-trace-dotnet'
    $baseUrl = "https://api.github.com/repos/$repo"

    # Step 1: Get PR head SHA
    Write-Verbose "Fetching PR #$PRNumber details from GitHub API..."
    try {
        $pr = Invoke-RestMethod -Uri "$baseUrl/pulls/$PRNumber" -ContentType 'application/json'
    }
    catch {
        throw "Failed to get PR #$PRNumber from GitHub API: $($_.Exception.Message)"
    }

    $headSha = $pr.head.sha
    if (-not $headSha) {
        throw "Could not get head SHA for PR #$PRNumber"
    }
    Write-Verbose "PR #$PRNumber head SHA: $headSha"

    # Step 2: Get commit statuses (where Azure DevOps checks appear)
    Write-Verbose "Fetching commit statuses for $headSha..."
    try {
        $statuses = Invoke-RestMethod -Uri "$baseUrl/commits/$headSha/statuses?per_page=100" -ContentType 'application/json'
    }
    catch {
        throw "Failed to get commit statuses from GitHub API: $($_.Exception.Message)"
    }

    # Find status with Azure DevOps URL
    $azureStatus = $statuses | Where-Object { $_.target_url -like '*dev.azure.com*' } | Select-Object -First 1

    if (-not $azureStatus) {
        throw "No Azure DevOps status found for PR #$PRNumber (SHA: $headSha)"
    }

    if ($azureStatus.target_url -match 'buildId=(\d+)') {
        $buildId = [int]$matches[1]
        Write-Verbose "Resolved build ID: $buildId from status: $($azureStatus.context)"
        return $buildId
    }

    throw "Could not extract build ID from URL: $($azureStatus.target_url)"
}

function Test-Prerequisites {
    <#
    .SYNOPSIS
        Validates that required CLI tools are available or that HTTP fallback can be used.

    .DESCRIPTION
        For read-only operations (GET), the Azure CLI and GitHub CLI are optional —
        direct HTTP requests work against public projects/repos. Mutating operations
        (stage retry) require the Azure CLI with authentication.

        When a CLI is missing or misconfigured, a warning is emitted and the script
        continues (the CLI call will fail at runtime and fall back to HTTP).

    .PARAMETER ParameterSetName
        The parameter set being used: 'ByBuildId', 'ByPullRequest', or 'ByCurrentBranch'.
        Determines which tools are needed.
    #>
    param(
        [string]$ParameterSetName = 'ByBuildId'
    )

    # Azure CLI: optional for read-only analysis (HTTP fallback exists for GET requests).
    # When present but misconfigured, warn — the CLI call will fail and fall back to HTTP.
    # Stage retry (PATCH) requires a working az CLI, but that's enforced at call time.
    $hasAz = [bool](Get-Command az -ErrorAction SilentlyContinue)
    if (-not $hasAz) {
        Write-Warning "Azure CLI (az) not found. Using direct HTTP for Azure DevOps API (read-only). Stage retry will not be available."
        Write-Warning "  Install: https://aka.ms/azure-cli"
    }
    else {
        # Warn about misconfiguration — the CLI functions will fall back to HTTP on failure
        $null = & az extension show --name azure-devops 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Azure CLI 'azure-devops' extension not found. CLI calls will fall back to direct HTTP."
            Write-Warning "  Install with: az extension add --name azure-devops"
        }

        $accountOutput = $null
        $accountOutput = & az account show --output json 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Azure CLI is not logged in. CLI calls will fall back to direct HTTP."
            Write-Warning "  Run: az login"
        }

        # Log current subscription for troubleshooting
        if ($accountOutput) {
            try {
                $account = $accountOutput | ConvertFrom-Json
                Write-Verbose "Current Azure subscription: '$($account.name)'"
            }
            catch {
                Write-Verbose "Could not parse Azure account info for subscription check: $_"
            }
        }
    }

    # GitHub CLI: optional for PR/branch resolution (HTTP fallback exists).
    # When present but not authenticated, warn — the functions will fall back to HTTP.
    $needsGh = $ParameterSetName -ne 'ByBuildId'
    if ($needsGh) {
        $hasGh = [bool](Get-Command gh -ErrorAction SilentlyContinue)
        if (-not $hasGh) {
            Write-Warning "GitHub CLI (gh) not found. Using GitHub REST API for PR resolution (rate-limited)."
            Write-Warning "  Install: https://cli.github.com"
        }
        else {
            $null = & gh auth status --hostname github.com 2>$null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "GitHub CLI is not authenticated. CLI calls will fall back to GitHub REST API."
                Write-Warning "  Run: gh auth login --hostname github.com"
            }
        }
    }
}

function Resolve-BuildId {
    <#
    .SYNOPSIS
        Resolves a build ID from the given parameter set (build ID, PR number, or current branch).

    .PARAMETER ParameterSetName
        The parameter set name: 'ByBuildId', 'ByPullRequest', or 'ByCurrentBranch'.

    .PARAMETER BuildId
        The build ID (used with 'ByBuildId').

    .PARAMETER PullRequest
        The PR number (used with 'ByPullRequest').
    #>
    param(
        [Parameter(Mandatory)]
        [string]$ParameterSetName,

        [int]$BuildId = 0,

        [int]$PullRequest = 0
    )

    Test-Prerequisites -ParameterSetName $ParameterSetName

    if ($ParameterSetName -eq 'ByBuildId') {
        return $BuildId
    }

    if ($ParameterSetName -eq 'ByCurrentBranch') {
        $PullRequest = Get-PRNumberForCurrentBranch
        Write-Verbose "Detected PR #$PullRequest for current branch."
    }

    return Get-BuildIdFromPR -PRNumber $PullRequest
}

function Get-PRNumberForCurrentBranch {
    <#
    .SYNOPSIS
        Gets the PR number for the current git branch.

    .DESCRIPTION
        Uses 'gh pr view' when the GitHub CLI is available. Falls back to the
        GitHub REST API (search open PRs by head branch) when gh is not installed.
    #>

    Write-Verbose "No arguments provided. Detecting PR for current branch..."

    $hasGh = [bool](Get-Command gh -ErrorAction SilentlyContinue)

    if ($hasGh) {
        Write-Verbose "Using gh CLI..."
        $stderrFile = [System.IO.Path]::GetTempFileName()
        try {
            $prOutput = & gh pr view --json number -q .number 2>$stderrFile
            if ($LASTEXITCODE -ne 0) {
                $stderr = Get-Content -Path $stderrFile -Raw -ErrorAction SilentlyContinue
                throw "gh pr view failed: $stderr"
            }
            return [int]$prOutput
        }
        catch {
            Write-Warning "GitHub CLI call failed, falling back to direct HTTP: $($_.Exception.Message)"
        }
        finally {
            Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
        }
    }
    else {
        Write-Verbose "GitHub CLI not found, using direct HTTP fallback"
    }

    # HTTP fallback: search GitHub API for open PRs matching current branch
    $branch = & git rev-parse --abbrev-ref HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $branch) {
        throw "Could not determine current git branch. Specify -PullRequest or -BuildId."
    }
    Write-Verbose "Current branch: $branch"

    $repo = 'DataDog/dd-trace-dotnet'
    $owner = 'DataDog'
    $encodedHead = [Uri]::EscapeDataString("${owner}:${branch}")
    $url = "https://api.github.com/repos/$repo/pulls?head=$encodedHead&state=all&per_page=1"

    Write-Verbose "Searching for open PRs with head=$encodedHead..."
    try {
        $prs = Invoke-RestMethod -Uri $url -ContentType 'application/json'
    }
    catch {
        throw "Failed to search PRs from GitHub API: $($_.Exception.Message)"
    }

    if (-not $prs -or $prs.Count -eq 0) {
        throw "No open PR found for branch '$branch'. Specify -PullRequest or -BuildId."
    }

    return [int]$prs[0].number
}

Export-ModuleMember -Function Invoke-AzDevOpsApi, Get-BuildIdFromPR, Resolve-BuildId, Test-Prerequisites
