param(
    [Parameter(Mandatory = $true)]
    [string] $AzureBuildId,
    [Parameter(Mandatory = $true)]
    [string] $AzureSourceVersion,
    [Parameter(Mandatory = $true)]
    [string] $AzureArtifactName,
    [Parameter(Mandatory = $true)]
    [string] $MonitoringHome,
    [Parameter(Mandatory = $true)]
    [string] $GitLabTriggerToken,
    [Parameter(Mandatory = $true)]
    [string] $GitLabReadToken,
    [string] $GitLabRef = 'master'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ($AzureBuildId -notmatch '^\d+$' -or $AzureSourceVersion -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Azure build metadata is malformed.'
}

if (-not (Test-Path -LiteralPath $MonitoringHome -PathType Container)) {
    throw "Monitoring home '$MonitoringHome' does not exist."
}

if ([string]::IsNullOrWhiteSpace($GitLabTriggerToken) -or [string]::IsNullOrWhiteSpace($GitLabReadToken)) {
    throw 'GitLab signing tokens are not configured.'
}

$gitLabApiBase = 'https://gitlab.ddbuild.io/api/v4'
$gitLabProject = 'DataDog%2Fdd-trace-dotnet'
$triggerUri = "$gitLabApiBase/projects/$gitLabProject/trigger/pipeline"
$triggerBody = @{
    token = $GitLabTriggerToken
    ref = $GitLabRef
    'variables[AZURE_WINDOWS_SIGNING]' = 'true'
    'variables[AZURE_BUILD_ID]' = $AzureBuildId
    'variables[AZURE_SOURCE_VERSION]' = $AzureSourceVersion
    'variables[AZURE_ARTIFACT_NAME]' = $AzureArtifactName
}

$pipeline = Invoke-RestMethod -Uri $triggerUri -Method Post -Body $triggerBody
$pipelineId = [string] $pipeline.id
if ($pipelineId -notmatch '^\d+$') {
    throw 'GitLab did not return a valid pipeline ID.'
}

Write-Output "Triggered GitLab Windows signing pipeline ${pipelineId}: $($pipeline.web_url)"

$headers = @{ 'PRIVATE-TOKEN' = $GitLabReadToken }
$pipelineUri = "$gitLabApiBase/projects/$gitLabProject/pipelines/$pipelineId"
$deadline = [DateTime]::UtcNow.AddMinutes(30)

do {
    Start-Sleep -Seconds 15
    $pipeline = Invoke-RestMethod -Uri $pipelineUri -Headers $headers -Method Get
    $status = [string] $pipeline.status
    Write-Output "GitLab signing pipeline $pipelineId status: $status"

    if ($status -in @('failed', 'canceled', 'skipped', 'manual')) {
        throw "GitLab signing pipeline $pipelineId finished with status '$status': $($pipeline.web_url)"
    }

    if ([DateTime]::UtcNow -ge $deadline) {
        throw "Timed out waiting for GitLab signing pipeline ${pipelineId}: $($pipeline.web_url)"
    }
} while ($status -ne 'success')

$jobs = Invoke-RestMethod -Uri "$pipelineUri/jobs?scope[]=success&per_page=100" -Headers $headers -Method Get
$signingJob = $jobs | Where-Object { $_.name -eq 'sign-azure-windows-artifacts' } | Select-Object -First 1
if ($null -eq $signingJob) {
    throw "Successful signing job was not found in GitLab pipeline $pipelineId."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dd-trace-signing-$AzureBuildId-$pipelineId"
$artifactZip = Join-Path $tempRoot 'gitlab-artifacts.zip'
$extractPath = Join-Path $tempRoot 'extracted'

if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $extractPath | Out-Null

try {
    $jobArtifactUri = "$gitLabApiBase/projects/$gitLabProject/jobs/$($signingJob.id)/artifacts"
    Invoke-WebRequest -Uri $jobArtifactUri -Headers $headers -OutFile $artifactZip
    Expand-Archive -LiteralPath $artifactZip -DestinationPath $extractPath

    $signedRoot = Join-Path $extractPath '.gitlab\azure-windows-signing-output'
    if (-not (Test-Path -LiteralPath $signedRoot -PathType Container)) {
        throw "GitLab signing artifact did not contain the expected output directory."
    }

    $expectedCertificateThumbprints = @(
        'A0FB7BEE153FE31431062731306903B3A5CB1824',
        '59063C826DAA5B628B5CE8A2B32015019F164BF0'
    )
    $requiredFiles = @(
        'win-x86\Datadog.Tracer.Native.dll',
        'win-x86\ddwaf.dll',
        'win-x64\datadog_profiling_ffi.dll'
    )
    $copiedFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    $signedFiles = Get-ChildItem -LiteralPath $signedRoot -Recurse -File |
        Where-Object { $_.Name -like 'Datadog*.dll' -or $_.Name -ieq 'ddwaf.dll' -or $_.Name -ieq 'datadog_profiling_ffi.dll' }

    foreach ($file in $signedFiles) {
        $relativePath = $file.FullName.Substring($signedRoot.Length).TrimStart('\', '/')
        if ($relativePath -notmatch '^win-(x86|x64)[\\/]') {
            throw "Unexpected signed artifact path '$relativePath'."
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Signature verification failed for '$relativePath': $($signature.Status)."
        }

        if ($expectedCertificateThumbprints -notcontains $signature.SignerCertificate.Thumbprint) {
            throw "Unexpected signing certificate for '$relativePath': $($signature.SignerCertificate.Thumbprint)."
        }

        $destination = Join-Path $MonitoringHome $relativePath
        if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
            throw "Refusing to introduce unexpected file '$relativePath' into the monitoring home."
        }

        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        [void] $copiedFiles.Add($relativePath)
    }

    foreach ($requiredFile in $requiredFiles) {
        if (-not $copiedFiles.Contains($requiredFile)) {
            throw "Required signed file '$requiredFile' was not returned by GitLab."
        }
    }

    Write-Output "Verified and copied $($copiedFiles.Count) signed Windows binaries into the monitoring home."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
