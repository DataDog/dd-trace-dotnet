$ErrorActionPreference = 'Stop'

$image = & "$PSScriptRoot/get-windows-build-image.ps1"
$targetBranch = if ($env:CI_MERGE_REQUEST_TARGET_BRANCH_NAME) {
    $env:CI_MERGE_REQUEST_TARGET_BRANCH_NAME
} else {
    $env:CI_DEFAULT_BRANCH
}
if (-not $targetBranch) {
    throw 'Unable to determine the comparison branch'
}

git fetch --no-tags origin "+refs/heads/${targetBranch}:refs/remotes/origin/${targetBranch}"
if ($LASTEXITCODE -ne 0) { throw 'Failed to fetch the comparison branch' }
$changedFiles = @(git diff --name-only "origin/${targetBranch}...HEAD")
if ($LASTEXITCODE -ne 0) { throw 'Failed to determine changed files' }

New-Item -ItemType Directory -Force .gitlab/generated | Out-Null
$changedFilesPath = Join-Path (Get-Location) '.gitlab/generated/changed-files.json'
[IO.File]::WriteAllText($changedFilesPath, (ConvertTo-Json -InputObject $changedFiles), (New-Object Text.UTF8Encoding $false))
$includeAll = $env:SHADOW_ALL_TEST_FRAMEWORKS -eq 'true'

docker run --rm -m 20480M `
    -v "$(Get-Location):c:\mnt" `
    -e CI=true `
    -e CI_JOB_ID `
    -e CI_COMMIT_BRANCH `
    -e CI_MERGE_REQUEST_SOURCE_BRANCH_NAME `
    -e CI_PROJECT_NAME `
    -e CI_JOB_NAME_SLUG `
    -e AWS_NETWORKING=true `
    -e WINDOWS_BUILDER=true `
    -e NUGET_CERT_REVOCATION_MODE=offline `
    -e CI_IDENTITIES_GITLAB_ID_TOKEN `
    $image `
    GenerateGitlabUnitTestsPipelines `
    --GitlabChangedFiles c:\mnt\.gitlab\generated\changed-files.json `
    --TargetBranch $targetBranch `
    --IncludeAllTestFrameworks $includeAll
if ($LASTEXITCODE -ne 0) { throw "Unit-test matrix generation failed ($LASTEXITCODE)" }
