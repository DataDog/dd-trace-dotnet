#!/usr/bin/env pwsh
#Requires -Version 5.1

<#
.SYNOPSIS
    Finds nuget.config by searching up the directory hierarchy.

.DESCRIPTION
    Starting from the specified directory, searches for nuget.config in the current
    directory and all parent directories up to the root.

.PARAMETER StartPath
    The directory to start searching from. Defaults to current directory.

.OUTPUTS
    Returns the full path to nuget.config if found, otherwise returns $null.

.EXAMPLE
    .\Find-NuGetConfig.ps1 -StartPath "D:\source\datadog\apm-serverless-test-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$StartPath = (Get-Location).Path
)

$currentDir = Get-Item -Path $StartPath -ErrorAction Stop

while ($currentDir) {
    $configPath = Join-Path $currentDir.FullName "nuget.config"

    if (Test-Path $configPath -PathType Leaf) {
        return $configPath
    }

    # Move to parent directory
    $currentDir = $currentDir.Parent
}

# Not found
return $null
