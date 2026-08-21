Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-RequiredEnvironmentVariable([string] $Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable '$Name' is not set."
    }

    return $value
}

$azureBuildId = Get-RequiredEnvironmentVariable 'AZURE_BUILD_ID'
$azureSourceVersion = Get-RequiredEnvironmentVariable 'AZURE_SOURCE_VERSION'
$azureArtifactName = Get-RequiredEnvironmentVariable 'AZURE_ARTIFACT_NAME'
$expectedAzureDefinitionId = '54'
$expectedAzureArtifactName = 'windows-signing-input'

if ($azureBuildId -notmatch '^\d+$' -or $azureSourceVersion -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Azure build metadata is malformed.'
}

if ($azureArtifactName -ne $expectedAzureArtifactName) {
    throw "Only the '$expectedAzureArtifactName' Azure artifact can be signed."
}

$azureApiBase = 'https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build'
$build = Invoke-RestMethod -Uri "$azureApiBase/builds/$azureBuildId`?api-version=7.1" -Method Get

if ([string] $build.definition.id -ne $expectedAzureDefinitionId) {
    throw "Azure build $azureBuildId belongs to pipeline $($build.definition.id), expected $expectedAzureDefinitionId."
}

if (-not [string]::Equals([string] $build.sourceVersion, $azureSourceVersion, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Azure build $azureBuildId belongs to commit $($build.sourceVersion), expected $azureSourceVersion."
}

if ([string] $build.sourceBranch -notmatch '^refs/heads/(master|main|release/.+|hotfix/.+)$') {
    throw "Azure build $azureBuildId belongs to untrusted branch '$($build.sourceBranch)'."
}

$artifact = Invoke-RestMethod -Uri "$azureApiBase/builds/$azureBuildId/artifacts?artifactName=$azureArtifactName&api-version=7.1" -Method Get
$downloadUrl = [string] $artifact.resource.downloadUrl
if ([string]::IsNullOrWhiteSpace($downloadUrl)) {
    throw "Azure artifact '$azureArtifactName' was not found on build $azureBuildId."
}

$downloadPath = Join-Path $PSScriptRoot 'azure-windows-signing-input.zip'
$extractPath = Join-Path $PSScriptRoot 'azure-windows-signing-input'
$outputPath = Join-Path $PSScriptRoot 'azure-windows-signing-output'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

Remove-Item -LiteralPath $downloadPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $extractPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $outputPath -Recurse -Force -ErrorAction SilentlyContinue

Invoke-WebRequest -Uri $downloadUrl -OutFile $downloadPath
Expand-Archive -LiteralPath $downloadPath -DestinationPath $extractPath

$artifactRoot = Join-Path $extractPath $azureArtifactName
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) {
    $artifactRoot = $extractPath
}

New-Item -ItemType Directory -Path $outputPath | Out-Null
Copy-Item -Path (Join-Path $artifactRoot '*') -Destination $outputPath -Recurse

$requiredFiles = @(
    'win-x86\Datadog.Tracer.Native.dll',
    'win-x86\ddwaf.dll',
    'win-x64\datadog_profiling_ffi.dll'
)

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $outputPath $relativePath) -PathType Leaf)) {
        throw "Required signing input '$relativePath' is missing from Azure artifact '$azureArtifactName'."
    }
}

$hash = & "$PSScriptRoot\..\tracer\build\_build\docker\gitlab\compute-image-hash.ps1"
if ($LASTEXITCODE -ne 0 -or $hash -notmatch '^[0-9a-f]{12}$') {
    throw "compute-image-hash.ps1 did not produce a valid hash (exit=$LASTEXITCODE, output='$hash')."
}

$image = "$($env:WINDOWS_BUILD_IMAGE_BASE):$hash"
docker manifest inspect $image | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Windows signing image '$image' does not exist."
}

docker run --rm -m 4096M `
    -v "${repositoryRoot}:c:\mnt" `
    -e CI_JOB_ID `
    -e AWS_NETWORKING=true `
    -e SIGN_WINDOWS=true `
    -e CI_IDENTITIES_GITLAB_ID_TOKEN `
    -e CI_PROJECT_NAME `
    -e CI_JOB_NAME_SLUG `
    $image `
    SignWindowsArtifacts `
    --WindowsSigningDirectory c:\mnt\.gitlab\azure-windows-signing-output

if ($LASTEXITCODE -ne 0) {
    throw "Windows signing container exited with code $LASTEXITCODE."
}

$expectedCertificateThumbprints = @(
    'A0FB7BEE153FE31431062731306903B3A5CB1824',
    '59063C826DAA5B628B5CE8A2B32015019F164BF0'
)

$signedFiles = Get-ChildItem -LiteralPath $outputPath -Recurse -File |
    Where-Object { $_.Name -like 'Datadog*.dll' -or $_.Name -ieq 'ddwaf.dll' -or $_.Name -ieq 'datadog_profiling_ffi.dll' }

foreach ($file in $signedFiles) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Signature verification failed for '$($file.FullName)': $($signature.Status)."
    }

    if ($expectedCertificateThumbprints -notcontains $signature.SignerCertificate.Thumbprint) {
        throw "Unexpected signing certificate for '$($file.FullName)': $($signature.SignerCertificate.Thumbprint)."
    }
}

Write-Output "Signed and verified $($signedFiles.Count) Windows binaries from Azure build $azureBuildId."
