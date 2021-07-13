Param (
    [String[]]
    $TracerVersions = ('1.30.0', '1.31.0', '2.0.0'),

    [Parameter(Mandatory)]
    [String]
    $SolutionDirectory,

    [Parameter(Mandatory)]
    [String]
    $DestinationDirectory
)

$ErrorActionPreference = 'Stop'

$SolutionDirectory = Resolve-Path $SolutionDirectory
$DestinationDirectory = Resolve-Path $DestinationDirectory
Push-Location "${SolutionDirectory}\build\tools\PrepareRelease"

try {
    Write-Output "Building PrepareRelease tool"
    dotnet build
    Write-Output ""

    foreach ($TracerVersion in $TracerVersions) {
        $TracerHomeDirectory = (Join-Path $DestinationDirectory "tracer-home-${TracerVersion}")

        # try this first because it can fail due to locked files, don't bother building if we can't delete $DestinationDirectory
        if (Test-Path -Path $TracerHomeDirectory -PathType 'Container') {
            Write-Output "Deleting ${TracerHomeDirectory}"
            Remove-Item -Path $TracerHomeDirectory -Recurse -Force
        }

        Write-Output "Changing tracer version to ${TracerVersion}"
        dotnet run --no-build versions "--version:${TracerVersion}"

        Write-Output "Building tracer ${TracerVersion}"
        nuke clean build-tracer-home package-tracer-home

        Write-Output "Copying `$SolutionDirectory\bin\tracer-home to ${TracerHomeDirectory}"
        Copy-Item -Path "${SolutionDirectory}\bin\tracer-home" -Destination $TracerHomeDirectory -Recurse -Force

        Write-Output "Copying `$SolutionDirectory\bin\artifacts\nuget\Datadog.Trace.${TracerVersion}.nupkg to ${DestinationDirectory}"
        Copy-Item -Path "${SolutionDirectory}\bin\artifacts\nuget\Datadog.Trace.${TracerVersion}.nupkg" -Destination $DestinationDirectory -Force

        Write-Output "Copying `$SolutionDirectory\bin\artifacts\x64\en-us\datadog-dotnet-apm-${TracerVersion}-x64.msi to ${DestinationDirectory}"
        Copy-Item -Path "${SolutionDirectory}\bin\artifacts\x64\en-us\datadog-dotnet-apm-${TracerVersion}-x64.msi" -Destination $DestinationDirectory -Force

        Write-Output ""
    }
}
finally {
    Pop-Location
}
