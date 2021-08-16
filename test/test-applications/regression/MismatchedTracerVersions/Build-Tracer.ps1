Param (
    [String[]]
    $TracerVersions = ('1.30.0', '1.31.0', '2.0.0'),

    [ValidateScript( { Test-Path $_ -PathType 'Container' })]
    [String]
    $SolutionDirectory = (Join-Path $PSScriptRoot '../../../..'),

    [String]
    $DestinationDirectory = (Join-Path $PSScriptRoot 'tracer-homes')
)

$ErrorActionPreference = 'Stop'

Resolve-Path "$SolutionDirectory/build.ps1" | Out-Null

if (!(Test-Path $DestinationDirectory)) {
    New-Item $DestinationDirectory -ItemType Directory -Force | Out-Null
}

foreach ($TracerVersion in $TracerVersions) {
    $TracerHomeDirectory = (Join-Path $DestinationDirectory ${TracerVersion})

    if (Test-Path $TracerHomeDirectory -PathType 'Container') {
        Write-Host "Deleting ${TracerHomeDirectory}"
        Remove-Item $TracerHomeDirectory -Recurse -Force
    }

    Write-Host "Changing tracer version to ${TracerVersion}"
    & "$SolutionDirectory/build.ps1" UpdateVersion -Version $TracerVersion -IsPrerelease $false -NoLogo -Verbosity minimal

    Write-Host "Building tracer ${TracerVersion} into ${TracerHomeDirectory}"
    & "$SolutionDirectory/build.ps1" Clean BuildTracerHome PackageTracerHome -Version $TracerVersion -TracerHome $TracerHomeDirectory -NoLogo -Verbosity minimal

    if ($IsWindows) {
        Write-Host "Copying `$SolutionDirectory/bin/artifacts/nuget/Datadog.Trace.${TracerVersion}.nupkg to ${DestinationDirectory}"
        Copy-Item -Path "${SolutionDirectory}/bin/artifacts/nuget/Datadog.Trace.${TracerVersion}.nupkg" -Destination $DestinationDirectory -Force

        Write-Host "Copying `$SolutionDirectory/bin/artifacts/x64/en-us/datadog-dotnet-apm-${TracerVersion}-x64.msi to ${DestinationDirectory}"
        Copy-Item -Path "${SolutionDirectory}/bin/artifacts/x64/en-us/datadog-dotnet-apm-${TracerVersion}-x64.msi" -Destination $DestinationDirectory -Force
    }

    Write-Host 'Done.'
}