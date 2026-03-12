#Requires -Version 5.1

<#
.SYNOPSIS
    Deploys a .NET Azure Function app with a locally-built Datadog.Trace.dll and optionally triggers it.

.DESCRIPTION
    Automates the deployment workflow:
    1. Publishes the sample app with `dotnet publish`
    2. Builds Datadog.Trace (net6.0) from local source (unless -SkipTracerBuild)
    3. Replaces Datadog.Trace.dll in the publish output with the locally-built version
    4. Zips the publish output
    5. Deploys to Azure with `az functionapp deployment source config-zip`
    6. Waits for the worker process to restart
    7. Triggers the function via HTTP and captures the execution timestamp

    The sample app should reference the released Datadog.AzureFunctions NuGet package from nuget.org.
    Only the managed tracer DLL (Datadog.Trace.dll) is replaced with a locally-built version for testing.

.PARAMETER AppName
    The Azure Function App name (e.g., "lucasp-premium-linux-isolated-aspnet-dev").

.PARAMETER ResourceGroup
    The Azure resource group containing the Function App.

.PARAMETER SampleAppPath
    Path to the sample application directory containing the .csproj file.

.PARAMETER TracerSourcePath
    Path to the Datadog.Trace project directory. Defaults to tracer/src/Datadog.Trace relative to this script.

.PARAMETER TargetFramework
    Target framework moniker for building Datadog.Trace. Default: net6.0.

.PARAMETER SkipTracerBuild
    Skip building Datadog.Trace (use a previously-built version from the build output directory).

.PARAMETER SkipWait
    Skip the wait period after deployment (not recommended - workers need time to restart).

.PARAMETER WaitSeconds
    Number of seconds to wait after deployment before triggering. Default: 60.

.PARAMETER TriggerUrl
    Custom HTTP trigger URL. If not specified, defaults to:
    https://<AppName>.azurewebsites.net/api/HttpTest

.PARAMETER SkipTrigger
    Skip triggering the HTTP endpoint after deployment.

.EXAMPLE
    .\Deploy-AzureFunction.ps1 -AppName "lucasp-premium-linux-isolated-aspnet-dev" `
        -ResourceGroup "lucas.pimentel" `
        -SampleAppPath "D:\source\datadog\apm-serverless-test-apps--dev\azure\functions\dotnet\isolated-dotnet8-aspnetcore"

.EXAMPLE
    # Skip tracer build (use previously built version)
    .\Deploy-AzureFunction.ps1 -AppName "lucasp-premium-linux-isolated-aspnet-dev" `
        -ResourceGroup "lucas.pimentel" `
        -SampleAppPath "D:\source\datadog\apm-serverless-test-apps--dev\azure\functions\dotnet\isolated-dotnet8-aspnetcore" `
        -SkipTracerBuild

.EXAMPLE
    # Pipeline with log analysis
    $deploy = .\Deploy-AzureFunction.ps1 -AppName "lucasp-premium-linux-isolated-aspnet-dev" `
        -ResourceGroup "lucas.pimentel" `
        -SampleAppPath "D:\source\datadog\apm-serverless-test-apps--dev\azure\functions\dotnet\isolated-dotnet8-aspnetcore"

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

    [string]$TracerSourcePath,

    [string]$TargetFramework = 'net6.0',

    [switch]$SkipTracerBuild,

    [switch]$SkipWait,

    [int]$WaitSeconds = 60,

    [string]$TriggerUrl,

    [switch]$SkipTrigger
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve paths
$SampleAppPath = (Resolve-Path $SampleAppPath).Path

if (-not $TracerSourcePath) {
    $TracerSourcePath = Join-Path $PSScriptRoot '..\src\Datadog.Trace'
}
$TracerSourcePath = (Resolve-Path $TracerSourcePath).Path

# Guard: Verify prerequisites
Write-Verbose "Verifying prerequisites..."

if (-not (Test-Path $SampleAppPath)) {
    throw "Sample app path does not exist: $SampleAppPath"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK (dotnet) not found. Install from: https://dotnet.microsoft.com/download"
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) not found. Install from: https://learn.microsoft.com/cli/azure/install-azure-cli"
}

# Create temp directories
$timestamp = Get-Date -Format 'yyyyMMddHHmmss'
$publishDir = Join-Path $env:TEMP "dd-azfunc-publish-$timestamp"
$zipPath = Join-Path $env:TEMP "dd-azfunc-deploy-$timestamp.zip"
$cleanupOnSuccess = @($publishDir, $zipPath)

try {
    # Step 1: Publish sample app
    Write-Host "Publishing sample app to: $publishDir" -ForegroundColor Cyan
    dotnet publish $SampleAppPath -c Release -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    # Verify host.json exists at root of publish output
    if (-not (Test-Path (Join-Path $publishDir 'host.json'))) {
        throw "Publish output does not contain host.json at root. Verify the sample app is a valid Azure Functions project."
    }

    # Step 2: Build Datadog.Trace
    $tracerDllBuildOutput = Join-Path $TracerSourcePath "bin\Release\$TargetFramework\Datadog.Trace.dll"

    if (-not $SkipTracerBuild) {
        Write-Host "Building Datadog.Trace ($TargetFramework)..." -ForegroundColor Cyan
        dotnet build $TracerSourcePath -c Release -f $TargetFramework
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build Datadog.Trace failed with exit code $LASTEXITCODE"
        }
    } else {
        Write-Verbose "Skipping tracer build (SkipTracerBuild specified)"
    }

    if (-not (Test-Path $tracerDllBuildOutput)) {
        throw "Datadog.Trace.dll not found at: $tracerDllBuildOutput. Build the tracer first or remove -SkipTracerBuild."
    }

    # Step 3: Replace Datadog.Trace.dll in publish output
    $tracerDllTarget = Join-Path $publishDir "datadog\$TargetFramework\Datadog.Trace.dll"
    if (-not (Test-Path $tracerDllTarget)) {
        throw "Target Datadog.Trace.dll not found in publish output at: $tracerDllTarget. Verify the sample app references the Datadog.AzureFunctions NuGet package."
    }

    Write-Host "Replacing Datadog.Trace.dll in publish output..." -ForegroundColor Cyan
    Write-Verbose "  Source: $tracerDllBuildOutput"
    Write-Verbose "  Target: $tracerDllTarget"
    Copy-Item -Path $tracerDllBuildOutput -Destination $tracerDllTarget -Force

    # Step 4: Zip the publish output
    Write-Host "Creating deployment zip: $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

    # Step 5: Deploy to Azure
    Write-Host "Deploying to Azure Function App: $AppName..." -ForegroundColor Cyan
    az functionapp deployment source config-zip -g $ResourceGroup -n $AppName --src $zipPath
    if ($LASTEXITCODE -ne 0) {
        throw "az functionapp deployment source config-zip failed with exit code $LASTEXITCODE"
    }

    # Step 6: Wait for worker restart
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

    # Step 7: Trigger function and capture timestamp
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
        $execTimestamp = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss")

        try {
            $response = Invoke-WebRequest -Uri $TriggerUrl -Method Get -UseBasicParsing
            $result.HttpStatus = $response.StatusCode
            $result.ExecutionTimestamp = $execTimestamp
            $result.TriggerUrl = $TriggerUrl

            Write-Host "Function triggered successfully (HTTP $($response.StatusCode))" -ForegroundColor Green
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.value__
            if ($statusCode) {
                $result.HttpStatus = $statusCode
                $result.ExecutionTimestamp = $execTimestamp
                $result.TriggerUrl = $TriggerUrl
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
    Write-Host "  Publish Dir:        $publishDir"
    Write-Host "  Zip File:           $zipPath"

    if ($result.ExecutionTimestamp) {
        Write-Host "`nNext: Analyze logs with:" -ForegroundColor Yellow
        Write-Host "  .\tracer\tools\Get-AzureFunctionLogs.ps1 -AppName '$AppName' -ResourceGroup '$ResourceGroup' -ExecutionTimestamp '$($result.ExecutionTimestamp)' -All" -ForegroundColor Gray
    }

    # Clean up temp files on success
    foreach ($path in $cleanupOnSuccess) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
            Write-Verbose "Cleaned up: $path"
        }
    }

    return $result

} catch {
    Write-Host "`nDeployment failed. Temp files kept for debugging:" -ForegroundColor Red
    Write-Host "  Publish Dir: $publishDir" -ForegroundColor Gray
    Write-Host "  Zip File:    $zipPath" -ForegroundColor Gray
    throw
}
