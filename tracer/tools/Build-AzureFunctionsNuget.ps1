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

.PARAMETER CopyTo
    Optional destination path. If provided, copies the built NuGet package to this location.

.EXAMPLE
    .\Build-AzureFunctionsNuget.ps1
    Build using existing bundle

.EXAMPLE
    .\Build-AzureFunctionsNuget.ps1 -BuildId 12345
    Download bundle from build first

.EXAMPLE
    .\Build-AzureFunctionsNuget.ps1 -CopyTo 'D:\temp\nuget'
    Build and copy package to specified path
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]
    $BuildId,

    [Parameter()]
    [string]
    $CopyTo
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Detect OS and determine build script
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $buildScript = if ($IsWindows) { 'build.ps1' } else { 'build.sh' }
} else {
    # PowerShell 5.x is Windows-only
    $buildScript = 'build.ps1'
}

# Resolve paths relative to script location
$scriptDir = Split-Path -Parent $PSCommandPath
$tracerDir = Split-Path -Parent $scriptDir
Write-Verbose "Tracer directory: $tracerDir"

# Clean up previous builds
Write-Verbose "Cleaning up previous builds from: $tracerDir/bin/artifacts/nuget/azure-functions/"
Remove-Item -Path "$tracerDir/bin/artifacts/nuget/azure-functions/*" -Force -Recurse -ErrorAction SilentlyContinue

# Remove package Datadog.AzureFunctions from NuGet cache
Write-Verbose "Removing $packageId from NuGet cache..."
$packageId = 'Datadog.AzureFunctions'
$globalPackagesPath = & dotnet nuget locals global-packages --list | ForEach-Object {
    if ($_ -match "global-packages:\s*(.+)$") { $matches[1] }
}
Write-Verbose "Global packages path: $globalPackagesPath"

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
        Write-Verbose "Package `"$packagePath`" not found in the NuGet cache."
    }
}

# Download Datadog.Trace.Bundle NuGet package from build artifacts
if ($BuildId)
{
    Write-Verbose "Downloading Datadog.Trace.Bundle from build: $BuildId"
    & "$tracerDir/$buildScript" DownloadBundleNugetFromBuild --build-id $BuildId
}
else
{
    Write-Verbose "Skipping bundle download (no BuildId provided)"
}

# Build Datadog.Trace and publish to bundle folder, replacing the files from the NuGet package
Write-Verbose "Publishing Datadog.Trace (net6.0) to bundle folder..."
dotnet publish "$tracerDir/src/Datadog.Trace" -c Release -o "$tracerDir/src/Datadog.Trace.Bundle/home/net6.0" -f 'net6.0'

Write-Verbose "Publishing Datadog.Trace (net461) to bundle folder..."
dotnet publish "$tracerDir/src/Datadog.Trace" -c Release -o "$tracerDir/src/Datadog.Trace.Bundle/home/net461" -f 'net461'

# Build Azure Functions NuGet package
Write-Verbose "Building Datadog.AzureFunctions NuGet package..."
dotnet restore "$tracerDir/src/Datadog.AzureFunctions"
& "$tracerDir/$buildScript" BuildAzureFunctionsNuget

# Copy package to destination if specified
if ($CopyTo)
{
    Write-Verbose "Copying package to: $CopyTo"
    Copy-Item "$tracerDir/bin/artifacts/nuget/azure-functions/Datadog.AzureFunctions.*.nupkg" $CopyTo -Force
}

Write-Verbose "Build complete!"
