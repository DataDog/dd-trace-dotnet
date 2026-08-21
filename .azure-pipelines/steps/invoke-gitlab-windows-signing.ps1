param(
    [Parameter(Mandatory = $true)]
    [string] $AzureBuildId,
    [Parameter(Mandatory = $true)]
    [string] $AzureSourceVersion,
    [Parameter(Mandatory = $true)]
    [string] $AzureSourceBranch,
    [Parameter(Mandatory = $true)]
    [string] $AzureArtifactName,
    [Parameter(Mandatory = $true)]
    [string] $MonitoringHome,
    [Parameter(Mandatory = $true)]
    [string] $GitLabApiToken
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

if ([string]::IsNullOrWhiteSpace($GitLabApiToken)) {
    throw 'The GitLab signing API token is not configured.'
}

$gitLabApiBase = 'https://gitlab.ddbuild.io/api/v4'
$gitLabProject = 'DataDog%2Fdd-trace-dotnet'
$headers = @{ 'PRIVATE-TOKEN' = $GitLabApiToken }
$gitLabRef = $AzureSourceBranch -replace '^refs/heads/', ''
$encodedRef = [Uri]::EscapeDataString($gitLabRef)
$pipelinesUri = "$gitLabApiBase/projects/$gitLabProject/pipelines?sha=$AzureSourceVersion&ref=$encodedRef&per_page=20&order_by=id&sort=desc"
$deadline = [DateTime]::UtcNow.AddMinutes(30)
$pipeline = $null
$signingJob = $null

do {
    $pipelines = Invoke-RestMethod -Uri $pipelinesUri -Headers $headers -Method Get
    $pipeline = $pipelines | Select-Object -First 1
    if ($null -ne $pipeline) {
        $pipelineId = [string] $pipeline.id
        $pipelineUri = "$gitLabApiBase/projects/$gitLabProject/pipelines/$pipelineId"
        $jobs = Invoke-RestMethod -Uri "$pipelineUri/jobs?per_page=100" -Headers $headers -Method Get
        $signingJob = $jobs | Where-Object { $_.name -eq 'sign-azure-windows-artifacts' } | Select-Object -First 1
    }

    if ([DateTime]::UtcNow -ge $deadline) {
        throw "Timed out waiting for the GitLab signing job for commit $AzureSourceVersion on '$gitLabRef'."
    }

    if ($null -eq $signingJob) {
        Write-Output "Waiting for the GitLab signing job for commit $AzureSourceVersion on '$gitLabRef'."
        Start-Sleep -Seconds 15
    }
} while ($null -eq $signingJob)

if ([string] $signingJob.status -ne 'manual') {
    throw "GitLab signing job $($signingJob.id) is '$($signingJob.status)', expected 'manual'. Each Azure build must use a fresh GitLab pipeline."
}

$playBody = @{
    job_variables_attributes = @(
        @{ key = 'AZURE_BUILD_ID'; value = $AzureBuildId },
        @{ key = 'AZURE_SOURCE_VERSION'; value = $AzureSourceVersion },
        @{ key = 'AZURE_ARTIFACT_NAME'; value = $AzureArtifactName }
    )
} | ConvertTo-Json -Depth 4

$signingJob = Invoke-RestMethod `
    -Uri "$gitLabApiBase/projects/$gitLabProject/jobs/$($signingJob.id)/play" `
    -Headers $headers `
    -Method Post `
    -ContentType 'application/json' `
    -Body $playBody

Write-Output "Started GitLab signing job $($signingJob.id): $($signingJob.web_url)"

do {
    Start-Sleep -Seconds 15
    $signingJob = Invoke-RestMethod -Uri "$gitLabApiBase/projects/$gitLabProject/jobs/$($signingJob.id)" -Headers $headers -Method Get
    $status = [string] $signingJob.status
    Write-Output "GitLab signing job $($signingJob.id) status: $status"

    if ($status -in @('failed', 'canceled', 'skipped', 'manual')) {
        throw "GitLab signing job $($signingJob.id) finished with status '$status': $($signingJob.web_url)"
    }

    if ([DateTime]::UtcNow -ge $deadline) {
        throw "Timed out waiting for GitLab signing job $($signingJob.id): $($signingJob.web_url)"
    }
} while ($status -ne 'success')

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
