param (
    [string]$TracerHomeDirectory,                              # Path to the compiled dd-trace-dotnet home directory
    [string]$DatadogTraceProjectFile,                          # Path to the Datadog.Trace.csproj project file
    [string]$OutputDirectory,                                  # Output directory
    [string]$PackageId = "Datadog.Serverless.AzureFunctions",  # Package ID
    [string]$Version = "0.0.1-alpha1",                         # Package version
    [bool]$RemoveNuGetCache = $True,                           # Remove Datadog.Serverless.AzureFunctions.<version>.nupkg from NuGet cache
    [bool]$CleanupTempFiles = $True                            # Cleanup temporary files
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if (-not $TracerHomeDirectory) {
    $TracerHomeDirectory = Get-ChildItem -Directory -Path "$PSScriptRoot" -Name "monitoring-home"
}

$TracerHomeDirectory = Resolve-Path $TracerHomeDirectory

if (-not (Test-Path -PathType Container $TracerHomeDirectory)) {
    Write-Error "Error: Default path `"$TracerHomeDirectory` not found."
    exit 1
}

# Find the Datadog.Trace project file
if ($DatadogTraceProjectFile) {
    $DatadogTraceProjectFile = Resolve-Path $DatadogTraceProjectFile

    # if $DatadogTraceProjectFile is provided, confirm it exists
    if (-not (Test-Path $DatadogTraceProjectFile)) {
        Write-Error "Error: Project file `"$DatadogTraceProjectFile`" not found."
        exit 1
    }
} else {
    # if $DatadogTraceProjectFile is not provided, search for it in the most probable location
    $DatadogTraceProjectFile = Get-ChildItem -Path "$PSScriptRoot/../Datadog.Trace/Datadog.Trace.csproj"

    if (-not (Test-Path $DatadogTraceProjectFile)) {
        Write-Error "Error: Datadog.Trace.csproj file not found in default path `"$DatadogTraceProjectFile`"."
        exit 1
    }
}

if ($OutputDirectory) {
    $OutputDirectory = Resolve-Path $OutputDirectory
} else {
    New-Item -ItemType Directory -Path "$PSScriptRoot/output" -Force | Out-Null
    $OutputDirectory = Resolve-Path "$PSScriptRoot/output/"
}

Write-Host "Using TracerHomeDirectory     = $TracerHomeDirectory"
Write-Host "Using DatadogTraceProjectFile = $DatadogTraceProjectFile"
Write-Host "Using OutputDirectory         = $OutputDirectory"
Write-Host "Using Version                 = $Version"
Write-Host

# Clean up previous artifacts
Write-Host "Cleaning up previous artifacts from `"$PSScriptRoot/temp`"."
Remove-Item -Path "$PSScriptRoot/temp" -Recurse -Force -ErrorAction SilentlyContinue

# Create the directories for the new package structure
$newPackagePath = "$PSScriptRoot/temp/$PackageId"
New-Item -ItemType Directory -Path $newPackagePath -Force | Out-Null
$newPackagePath = Resolve-Path $newPackagePath
Write-Host "Copying files into `"$newPackagePath`"."

# Copy native files from the bundle to the new package structure
# $rids = @("linux-arm64", "linux-x64", "win-x64", "win-x86")
$rids = @("win-x64")
$filenames = @("Datadog.Trace.ClrProfiler.Native", "Datadog.Tracer.Native")

foreach ($rid in $rids) {
    $fileNameExtension = if ($rid -like "win-*") { "dll" } elseif ($rid -like "linux-*") { "so" }

    # Create directory
    New-Item -ItemType Directory -Path "$newPackagePath/$rid" -Force | Out-Null

    # Copy native libraries
    foreach ($filename in $filenames) {
        Copy-Item -Force `
            -Path "$TracerHomeDirectory/$rid/$filename.$fileNameExtension" `
            -Destination "$newPackagePath/$rid/$filename.$fileNameExtension"
    }

    # Copy loader.conf
    Copy-Item -Force `
            -Path "$TracerHomeDirectory/$rid/loader.conf" `
            -Destination "$newPackagePath/$rid/"
}

# Build and copy Datadog.Trace.dll for each target framework moniker (TFM)
# $tfms = @("net6.0", "net461")
$tfms = @("net6.0")

foreach ($tfm in $tfms) {
    Write-Host "Building `"Datadog.Trace.dll`" for `"$tfm`"."
    $tfmBuildOutputDirectory = "$PSScriptRoot/temp/Datadog.Trace/$tfm"
    $output = dotnet publish "$DatadogTraceProjectFile" -tl:off --configuration Release --framework $tfm --output "$tfmBuildOutputDirectory" 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to build `"$DatadogTraceProjectFile`" for `"$tfm`"."
        $output | ForEach-Object { Write-Host $_ }
        exit 1
    }

    # Create directory
    $destinationDirectory = "$newPackagePath/$tfm"
    New-Item -ItemType Directory -Path "$destinationDirectory" -Force | Out-Null

    # Copy managed library
    Copy-Item -Force `
        -Path "$tfmBuildOutputDirectory/Datadog.Trace.dll" `
        -Destination "$destinationDirectory/Datadog.Trace.dll"
}

# Create agent directory
New-Item -ItemType Directory -Path "$newPackagePath/agent/win-x64" -Force | Out-Null

# Copy trace-agent-<version>.exe and configuration file
Copy-Item -Force `
            -Path "$TracerHomeDirectory/*trace-agent*.exe" `
            -Destination "$newPackagePath/agent/win-x64/datadog-trace-agent.exe"

Copy-Item -Force `
            -Path "$TracerHomeDirectory/datadog.yaml" `
            -Destination "$newPackagePath/agent/win-x64/datadog.yaml"

# Pack the NuGet package
$nugetPackagePath = "$OutputDirectory/$PackageId.$Version.nupkg"
Write-Host "Packing `"$PSScriptRoot/Datadog.Serverless.AzureFunctions.csproj`"."
Write-Host "   into `"$nugetPackagePath`"."
Remove-Item -Path "$nugetPackagePath" -Force -ErrorAction SilentlyContinue
$output = dotnet pack "$PSScriptRoot/Datadog.Serverless.AzureFunctions.csproj" -tl:off --property:Version=$Version --output "$OutputDirectory" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error: Failed to pack `"$PSScriptRoot/Datadog.Serverless.AzureFunctions.csproj`""
    $output | ForEach-Object { Write-Host $_ }
    exit 1
}

# Remove the package from the NuGet cache
if ($RemoveNuGetCache) {
    Write-Host "Removing `"$PackageId`" from NuGet cache."

    # Get the global packages folder path
    $globalPackagesPath = & dotnet nuget locals global-packages --list | ForEach-Object {
        if ($_ -match "global-packages:\s*(.+)$") { $matches[1] }
    }

    if (-not $globalPackagesPath)
    {
        Write-Warning "Warning: Failed to find the global packages folder."
    }
    else
    {
        $packagePath = Join-Path -Path $globalPackagesPath -ChildPath $PackageId

        if (Test-Path $packagePath)
        {
            Write-Host "Deleting `"$packagePath`"."
            Remove-Item -Path $packagePath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($CleanupTempFiles)
{
    Write-Host "Deleting temporary directory `"$PSScriptRoot/temp`"."
    Remove-Item -Path "$PSScriptRoot/temp" -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Done."
