param (
    [string]$BundleNupkgFile,           # Path to the Datadog.Trace.Bundle nupkg file
    [string]$DatadogTraceProjectFile,   # Path to the Datadog.Trace.csproj project file
    [string]$OutputDirectory,           # Output directory
    [string]$Version = "0.0.1-alpha1"   # Package version
)

# Exit on error
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Find the Datadog.Trace.Bundle nupkg file
if ($BundleNupkgFile) {
    $BundleNupkgFile = Resolve-Path $BundleNupkgFile

    # if $BundleNupkgFile is provided, confirm it exists
    if (-not (Test-Path $BundleNupkgFile)) {
        Write-Host "Error: Datadog.Trace.Bundle nupkg not found in the specified path $BundleNupkgFile"
        exit 1
    }
} else {
    # if $BundleNupkgFile is not provided, search for it in the script directory
    $bundleNupkgFiles = @(Get-ChildItem -Path "$PSScriptRoot" -Filter "Datadog.Trace.Bundle.*.nupkg")

    if ($bundleNupkgFiles.Length -eq 0) {
        Write-Host "Error: Datadog.Trace.Bundle.<version>.nupkg file not found in default path $PSScriptRoot."
        exit 1
    }
    elseif ($bundleNupkgFiles.Length -gt 1) {
        Write-Host "Error: Multiple Datadog.Trace.Bundle.<version>.nupkg files were found in default path $PSScriptRoot."
        exit 1
    }

    $BundleNupkgFile = $bundleNupkgFiles[0]
}

# Find the Datadog.Trace project file
if ($DatadogTraceProjectFile) {
    $DatadogTraceProjectFile = Resolve-Path $DatadogTraceProjectFile

    # if $DatadogTraceProjectFile is provided, confirm it exists
    if (-not (Test-Path $DatadogTraceProjectFile)) {
        Write-Host "Error: Datadog.Trace project file not found in the specified path $DatadogTraceProjectFile."
        exit 1
    }
} else {
    # if $DatadogTraceProjectFile is not provided, search for it in the most probable location
    $DatadogTraceProjectFile = Get-ChildItem -Path "$PSScriptRoot/../dd-trace-dotnet/tracer/src/Datadog.Trace/Datadog.Trace.csproj"

    if (-not $DatadogTraceProjectFile) {
        Write-Host "Error: Datadog.Trace.csproj file not found in default path $DatadogTraceProjectFile."
        exit 1
    }
}

if ($OutputDirectory) {
    $OutputDirectory = Resolve-Path $OutputDirectory
} else {
    New-Item -ItemType Directory -Path "$PSScriptRoot/output" -Force | Out-Null
    $OutputDirectory = Resolve-Path "$PSScriptRoot/output"
}

Write-Host "Using BundleNupkgFile = $BundleNupkgFile"
Write-Host "Using DatadogTraceProjectFile = $DatadogTraceProjectFile"
Write-Host "Using OutputDirectory = $OutputDirectory"
Write-Host "Using Version = $Version"
Write-Host

# Clean up previous artifacts
Write-Host "Cleaning up previous artifacts from $PSScriptRoot/temp."
Remove-Item -Path "$PSScriptRoot/temp" -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue

# Unzip the bundle
$bundleUnzipPath = "$PSScriptRoot/temp/Datadog.Trace.Bundle"
New-Item -ItemType Directory -Path $bundleUnzipPath -Force | Out-Null
$bundleUnzipPath = Resolve-Path $bundleUnzipPath
Write-Host "Unzipping $BundleNupkgFile"
Write-Host "     into $bundleUnzipPath."
Expand-Archive -Path $BundleNupkgFile -DestinationPath $bundleUnzipPath -Force

# Copy native files from the bundle to the new package structure
$newPackagePath = "$PSScriptRoot/temp/Datadog.Serverless.AzureFunctions"
New-Item -ItemType Directory -Path $newPackagePath -Force | Out-Null
$newPackagePath = Resolve-Path $newPackagePath
Write-Host "Copying files into $newPackagePath."

$folders = @("linux-arm64", "linux-x64", "win-x64", "win-x86", "net6.0", "net461")

foreach ($folder in $folders) {
    New-Item -ItemType Directory -Path "$newPackagePath/$folder" -Force | Out-Null
}

$ridMapping = @(
    @{ Rid = "linux-arm64"; FileNameExtension = "so" },
    @{ Rid = "linux-x64"; FileNameExtension = "so" },
    @{ Rid = "win-x64"; FileNameExtension = "dll" },
    @{ Rid = "win-x86"; FileNameExtension = "dll" }
)

$filenames = @("Datadog.Trace.ClrProfiler.Native", "Datadog.Tracer.Native")

foreach ($filename in $filenames) {
    foreach ($mapping in $ridMapping) {
        $rid = $mapping.Rid
        $fileNameExtension = $mapping.FileNameExtension
        $sourcePath = "$bundleUnzipPath/content/datadog/$rid/$filename.$fileNameExtension"
        $destinationPath = "$newPackagePath/$rid/$filename.$fileNameExtension"
        Copy-Item -Path $sourcePath -Destination $destinationPath -Force
    }
}

# Build and copy Datadog.Trace.dll for each target framework moniker (TFM)
$tfms = @("net6.0", "net461")

foreach ($tfm in $tfms) {
    Write-Host "Building Datadog.Trace.dll for $tfm."
    $output = dotnet publish $DatadogTraceProjectFile -tl:off --configuration Release --framework $tfm --output "$PSScriptRoot/temp/Datadog.Trace/$tfm" 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to build $DatadogTraceProjectFile for $tfm."
        $output | ForEach-Object { Write-Host $_ }
        exit 1
    }

    # Copy managed assemblies
    $sourcePath = "$PSScriptRoot/temp/Datadog.Trace/$tfm/Datadog.Trace.dll"
    $destinationPath = "$newPackagePath/$tfm/Datadog.Trace.dll"
    Copy-Item -Path $sourcePath -Destination $destinationPath -Force
}

# Pack the NuGet package
Write-Host "Packing new Datadog.Serverless.AzureFunctions nupkg into $OutputDirectory."
Remove-Item -Path "$OutputDirectory/Datadog.Serverless.AzureFunctions.${Version}.nupkg" -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
$output = dotnet pack "$PSScriptRoot/Datadog.Serverless.AzureFunctions.csproj" -tl:off --property:Version=$Version --output "$OutputDirectory" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to pack $PSScriptRoot/Datadog.Serverless.AzureFunctions.csproj"
    $output | ForEach-Object { Write-Host $_ }
    exit 1
}

# Write-Host "Cleaning up artifacts..."
# Remove-Item -Path "$PSScriptRoot/temp" -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue

Write-Host "Done."