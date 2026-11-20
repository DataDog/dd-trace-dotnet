# Returns the content-addressed Windows build image without contacting the registry.
$ErrorActionPreference = 'Stop'

$imageBase = $env:WINDOWS_BUILD_IMAGE_BASE
if ([string]::IsNullOrWhiteSpace($imageBase)) {
    throw 'WINDOWS_BUILD_IMAGE_BASE must be configured or forwarded by the parent pipeline'
}

$hash = @(& "$PSScriptRoot/../tracer/build/_build/docker/gitlab/compute-image-hash.ps1")
if ($LASTEXITCODE -ne 0 -or $hash.Count -ne 1 -or $hash[0] -cnotmatch '^[0-9a-f]{12}$') {
    throw "compute-image-hash.ps1 did not produce a valid hash (exit=$LASTEXITCODE, output='$hash')"
}

Write-Output "${imageBase}:$($hash[0])"
