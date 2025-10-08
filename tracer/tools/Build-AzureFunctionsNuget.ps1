<#
.SYNOPSIS
    Builds the Datadog.AzureFunctions NuGet package for local testing.

.DESCRIPTION
    This script allows you to test changes to Datadog.Trace in Azure Functions projects
    without waiting for a full CI build. It can optionally download the Datadog.Trace.Bundle
    package from a specific build, then replaces the managed tracer files with a locally
    built version before packaging Datadog.AzureFunctions.

.PARAMETER BuildId
    Optional Azure DevOps build ID. If provided, downloads the Datadog.Trace.Bundle package
    from the specified build before packaging.

.EXAMPLE
    .\Build-AzureFunctionsNuget.ps1
    Build using existing bundle

.EXAMPLE
    .\Build-AzureFunctionsNuget.ps1 -BuildId 12345
    Download bundle from build first
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]
    $BuildId
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Resolve paths relative to script location
$scriptDir = Split-Path -Parent $PSCommandPath
$tracerDir = Split-Path -Parent $scriptDir

# Clean up previous builds
Remove-Item -Path "$tracerDir\bin\artifacts\nuget\azure-functions\*" -Force -ErrorAction SilentlyContinue

# Remove package Datadog.AzureFunctions from NuGet cache
$packageId = 'Datadog.AzureFunctions'
$globalPackagesPath = & dotnet nuget locals global-packages --list | ForEach-Object {
    if ($_ -match "global-packages:\s*(.+)$") { $matches[1] }
}

if (-not $globalPackagesPath)
{
    Write-Warning "Failed to find the global packages folder."
}
else
{
    $packagePath = Join-Path -Path $globalPackagesPath -ChildPath $packageId

    if (Test-Path $packagePath)
    {
        Write-Host "Deleting `"$packagePath`"."
        Remove-Item -Path $packagePath -Recurse -Force -ErrorAction SilentlyContinue
    }
    else
    {
        Write-Host "Package `"$packagePath`" not found in the NuGet cache."
    }
}

# Download Datadog.Trace.Bundle NuGet package from build artifacts
if ($BuildId)
{
    & "$tracerDir\build.ps1" DownloadBundleNugetFromBuild --build-id $BuildId
}

# Build Datadog.Trace and publish to bundle folder, replacing the files from the NuGet package
dotnet publish "$tracerDir\src\Datadog.Trace" -c Release -o "$tracerDir\src\Datadog.Trace.Bundle\home\net6.0" -f 'net6.0'
dotnet publish "$tracerDir\src\Datadog.Trace" -c Release -o "$tracerDir\src\Datadog.Trace.Bundle\home\net461" -f 'net461'

# Build Azure Functions NuGet package
& "$tracerDir\build.ps1" BuildAzureFunctionsNuget