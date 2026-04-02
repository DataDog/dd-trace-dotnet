#Requires -Version 5.1

<#
.SYNOPSIS
    Shared helper functions for Azure DevOps CI scripts.

.DESCRIPTION
    Provides common functions for interacting with the Azure DevOps REST API
    via the Azure CLI. Used by Get-AzureDevOpsBuildAnalysis.ps1 and
    Retry-AzureDevOpsFailedStages.ps1.
#>

function Invoke-AzDevOpsApi {
    <#
    .SYNOPSIS
        Invokes an Azure DevOps REST API endpoint via 'az devops invoke'.

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

    # Build argument array to avoid command injection via Invoke-Expression
    $azArgs = @(
        'devops', 'invoke',
        '--area', $Area,
        '--resource', $Resource,
        '--org', 'https://dev.azure.com/datadoghq',
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

    $json = $output | ConvertFrom-Json

    if ($SaveToFile) {
        $json | ConvertTo-Json -Depth 100 | Set-Content -Path $SaveToFile -Encoding UTF8
        Write-Verbose "Saved to: $SaveToFile"
    }

    return $json
}

function Get-BuildIdFromPR {
    <#
    .SYNOPSIS
        Resolves an Azure DevOps build ID from a GitHub PR number.

    .PARAMETER PRNumber
        The GitHub pull request number.
    #>
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

function Test-Prerequisites {
    <#
    .SYNOPSIS
        Validates that required CLI tools are installed, authenticated, and properly configured.

    .DESCRIPTION
        Collects all prerequisite issues and reports them together so the user can fix
        everything in one pass rather than discovering problems one at a time.

    .PARAMETER NeedsGh
        When set, also validates that the GitHub CLI (gh) is installed and authenticated.
        Required for PR-based build resolution.
    #>
    param(
        [switch]$NeedsGh
    )

    $errors = @()

    # 1. Azure CLI installed
    $hasAz = [bool](Get-Command az -ErrorAction SilentlyContinue)
    if (-not $hasAz) {
        $errors += @"
Azure CLI (az) not found.
  Install: https://aka.ms/azure-cli
  Windows: winget install Microsoft.AzureCLI
  macOS:   brew install azure-cli
"@
    }

    if ($hasAz) {
        # 2. azure-devops extension installed
        $stderrFile = [System.IO.Path]::GetTempFileName()
        try {
            $null = & az extension show --name azure-devops 2>$stderrFile
            if ($LASTEXITCODE -ne 0) {
                $errors += @"
Azure CLI 'azure-devops' extension not found.
  Install with: az extension add --name azure-devops
"@
            }
        }
        finally {
            Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
        }

        # 3. Azure CLI authenticated
        $stderrFile = [System.IO.Path]::GetTempFileName()
        $accountOutput = $null
        try {
            $accountOutput = & az account show --output json 2>$stderrFile
            if ($LASTEXITCODE -ne 0) {
                $errors += @"
Azure CLI is not logged in.
  Run: az login
  For MFA-enabled tenants: az login --tenant <TENANT_ID> --use-device-code
"@
            }
        }
        finally {
            Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
        }

        # 4. Check subscription (warn only — the --org flag targets the org directly,
        #    but the wrong subscription can affect token permissions)
        if ($accountOutput) {
            try {
                $account = $accountOutput | ConvertFrom-Json
                $expectedSubscription = 'apm-libraries-build-and-sandbox'
                if ($account.name -ne $expectedSubscription) {
                    Write-Warning @"
Current Azure subscription is '$($account.name)'.
  For Azure DevOps access, you may need: az account set --subscription '$expectedSubscription'
"@
                }
            }
            catch {
                # Non-fatal: if we can't parse the account info, just continue
                Write-Verbose "Could not parse Azure account info for subscription check: $_"
            }
        }
    }

    if ($NeedsGh) {
        # 5. GitHub CLI installed
        $hasGh = [bool](Get-Command gh -ErrorAction SilentlyContinue)
        if (-not $hasGh) {
            $errors += @"
GitHub CLI (gh) not found.
  Install: https://cli.github.com
  Windows: winget install GitHub.cli
  macOS:   brew install gh
"@
        }

        # 6. GitHub CLI authenticated
        if ($hasGh) {
            $stderrFile = [System.IO.Path]::GetTempFileName()
            try {
                $null = & gh auth status 2>$stderrFile
                if ($LASTEXITCODE -ne 0) {
                    $errors += @"
GitHub CLI is not authenticated.
  Run: gh auth login
"@
                }
            }
            finally {
                Remove-Item -Path $stderrFile -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($errors.Count -gt 0) {
        $separator = "`n`n"
        throw "Prerequisite check failed:`n`n$($errors -join $separator)"
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

    $needsGh = $ParameterSetName -ne 'ByBuildId'
    Test-Prerequisites -NeedsGh:$needsGh

    if ($ParameterSetName -eq 'ByBuildId') {
        return $BuildId
    }

    if ($ParameterSetName -eq 'ByCurrentBranch') {
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

    return Get-BuildIdFromPR -PRNumber $PullRequest
}

Export-ModuleMember -Function Invoke-AzDevOpsApi, Get-BuildIdFromPR, Resolve-BuildId, Test-Prerequisites
