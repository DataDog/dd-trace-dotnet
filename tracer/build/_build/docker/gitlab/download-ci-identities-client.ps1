# Downloads the ci-identities GitLab job client binary from S3 into this
# directory (tracer/build/_build/docker/gitlab/).
#
# The URL is pinned here so version bumps contribute to the content hash of
# the Windows build image via compute-image-hash.ps1 — bumping the URL
# therefore invalidates the image tag and forces a rebuild of
# build-windows-ci-image. The downloaded .exe itself is excluded from the
# hash (it's an artifact, not source).

$ErrorActionPreference = 'Stop'

$url = 's3://binaries-ddbuild-io-prod/ci-identities/ci-identities-gitlab-job-client/versions/v0.2.0/ci-identities-gitlab-job-client-windows-amd64.exe'
$dest = Join-Path $PSScriptRoot 'ci-identities-gitlab-job-client.exe'

try {
    Write-Output "Downloading ci-identities client from $url to $dest"
    aws s3 cp --only-show-errors $url $dest
    if ($LASTEXITCODE -ne 0) { throw "aws s3 cp failed (exit=$LASTEXITCODE)" }
    exit 0
}
catch {
    [Console]::Error.WriteLine("download-ci-identities-client.ps1: $_")
    exit 1
}
