#Requires -Version 5.1

<#
.SYNOPSIS
    Deploys a .NET Azure Function app and optionally triggers it.

.DESCRIPTION
    Automates the deployment workflow: restores dependencies, publishes to Azure,
    waits for the worker process to restart, triggers the function via HTTP, and
    captures the execution timestamp for log analysis.

.PARAMETER AppName
    The Azure Function App name (e.g., "lucasp-premium-linux-isolated-aspnet").

.PARAMETER ResourceGroup
    The Azure resource group containing the Function App.

.PARAMETER SampleAppPath
    Path to the sample application directory containing the .csproj file.

.PARAMETER SkipBuild
    Skip running 'dotnet restore' before deployment.

.PARAMETER SkipWait
    Skip the wait period after deployment (not recommended - workers need time to restart).

.PARAMETER WaitSeconds
    Number of seconds to wait after deployment before triggering. Default: 120.

.PARAMETER TriggerUrl
    Custom HTTP trigger URL. If not specified, defaults to:
    https://<AppName>.azurewebsites.net/api/HttpTest

.PARAMETER SkipTrigger
    Skip triggering the HTTP endpoint after deployment.

.EXAMPLE
    .\Deploy-AzureFunction.ps1 -AppName "lucasp-premium-linux-isolated-aspnet" `
        -ResourceGroup "lucas.pimentel" `
        -SampleAppPath "D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore"

.EXAMPLE
    # Pipeline with log analysis
    $deploy = .\Deploy-AzureFunction.ps1 -AppName "lucasp-premium-linux-isolated-aspnet" `
        -ResourceGroup "lucas.pimentel" `
        -SampleAppPath "D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore"

    .\Get-AzureFunctionLogs.ps1 -AppName $deploy.AppName `
        -ResourceGroup "lucas.pimentel" `
        -ExecutionTimestamp $deploy.ExecutionTimestamp `
        -All

.OUTPUTS
    PSCustomObject with properties:
    - AppName: Function App name
    - ExecutionTimestamp: UTC timestamp when function was triggered (yyyy-MM-dd HH:mm:ss)
    - TriggerUrl: HTTP endpoint that was invoked
    - HttpStatus: HTTP status code from trigger response
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AppName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$SampleAppPath,

    [switch]$SkipBuild,

    [switch]$SkipWait,

    [int]$WaitSeconds = 120,

    [string]$TriggerUrl,

    [switch]$SkipTrigger
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Guard: Verify prerequisites
Write-Verbose "Verifying prerequisites..."

if (-not (Test-Path $SampleAppPath)) {
    throw "Sample app path does not exist: $SampleAppPath"
}

if (-not (Get-Command func -ErrorAction SilentlyContinue)) {
    throw "Azure Functions Core Tools (func) not found. Install from: https://docs.microsoft.com/azure/azure-functions/functions-run-local"
}

if (-not $SkipBuild -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK (dotnet) not found. Install from: https://dotnet.microsoft.com/download"
}

# Navigate to sample app directory
Write-Verbose "Navigating to sample app: $SampleAppPath"
Push-Location $SampleAppPath
try {
    # Step 1: Restore dependencies
    if (-not $SkipBuild) {
        Write-Host "Restoring dependencies..." -ForegroundColor Cyan
        dotnet restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed with exit code $LASTEXITCODE"
        }
    } else {
        Write-Verbose "Skipping dotnet restore (SkipBuild specified)"
    }

    # Step 2: Publish to Azure
    Write-Host "Publishing to Azure Function App: $AppName..." -ForegroundColor Cyan
    func azure functionapp publish $AppName
    if ($LASTEXITCODE -ne 0) {
        throw "func azure functionapp publish failed with exit code $LASTEXITCODE"
    }

    # Step 3: Wait for worker restart
    if (-not $SkipWait) {
        Write-Host "Waiting $WaitSeconds seconds for worker process to restart..." -ForegroundColor Yellow
        for ($i = $WaitSeconds; $i -gt 0; $i--) {
            Write-Progress -Activity "Waiting for worker restart" -Status "$i seconds remaining" -PercentComplete ((($WaitSeconds - $i) / $WaitSeconds) * 100)
            Start-Sleep -Seconds 1
        }
        Write-Progress -Activity "Waiting for worker restart" -Completed
    } else {
        Write-Verbose "Skipping wait period (SkipWait specified)"
    }

    # Step 4: Trigger function and capture timestamp
    $result = [PSCustomObject]@{
        AppName = $AppName
        ExecutionTimestamp = $null
        TriggerUrl = $null
        HttpStatus = $null
    }

    if (-not $SkipTrigger) {
        # Derive trigger URL if not provided
        if (-not $TriggerUrl) {
            $TriggerUrl = "https://$AppName.azurewebsites.net/api/HttpTest"
        }

        Write-Host "Triggering function at: $TriggerUrl" -ForegroundColor Cyan

        # Capture timestamp BEFORE triggering
        $timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss")

        try {
            $response = Invoke-WebRequest -Uri $TriggerUrl -Method Get -UseBasicParsing
            $result.HttpStatus = $response.StatusCode
            $result.ExecutionTimestamp = $timestamp
            $result.TriggerUrl = $TriggerUrl

            Write-Host "Function triggered successfully (HTTP $($response.StatusCode))" -ForegroundColor Green
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.value__
            if ($statusCode) {
                $result.HttpStatus = $statusCode
                Write-Warning "Function returned HTTP $statusCode"
            } else {
                Write-Error "Failed to trigger function: $_"
                throw
            }
        }
    } else {
        Write-Verbose "Skipping trigger (SkipTrigger specified)"
    }

    # Output summary
    Write-Host "`nDeployment Summary:" -ForegroundColor Cyan
    Write-Host "  App Name:           $($result.AppName)"
    Write-Host "  Execution Time:     $($result.ExecutionTimestamp)"
    Write-Host "  Trigger URL:        $($result.TriggerUrl)"
    Write-Host "  HTTP Status:        $($result.HttpStatus)"

    if ($result.ExecutionTimestamp) {
        Write-Host "`nNext: Analyze logs with:" -ForegroundColor Yellow
        Write-Host "  .\tracer\tools\Get-AzureFunctionLogs.ps1 -AppName '$AppName' -ResourceGroup '$ResourceGroup' -ExecutionTimestamp '$($result.ExecutionTimestamp)' -All" -ForegroundColor Gray
    }

    return $result

} finally {
    Pop-Location
}
