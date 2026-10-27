$ErrorActionPreference = 'Stop'

$image = & "$PSScriptRoot/get-windows-build-image.ps1"
$dockerArguments = @(
    'run', '--rm', '-m', '20480M',
    '-v', "$(Get-Location):c:\mnt",
    '-e', 'CI=true',
    '-e', 'GITLAB_CI',
    '-e', 'CI_JOB_ID',
    '-e', 'CI_COMMIT_BRANCH',
    '-e', 'CI_MERGE_REQUEST_SOURCE_BRANCH_NAME',
    '-e', 'CI_PROJECT_NAME',
    '-e', 'CI_JOB_NAME_SLUG',
    '-e', 'AWS_NETWORKING=true',
    '-e', 'WINDOWS_BUILDER=true',
    '-e', 'NUGET_CERT_REVOCATION_MODE=offline',
    $image
)
$branchArguments = @()
if ($env:CI_MERGE_REQUEST_TARGET_BRANCH_NAME) {
    $branchArguments = @('--TargetBranch', $env:CI_MERGE_REQUEST_TARGET_BRANCH_NAME)
}

# NUKE owns the version-to-release-branch mapping; Git stays on the runner because
# its checkout may use alternates that are inaccessible inside the container.
& docker @dockerArguments WriteGitlabUnitTestsBaseBranch @branchArguments
if ($LASTEXITCODE -ne 0) { throw "Comparison branch resolution failed ($LASTEXITCODE)" }
$baseBranch = (Get-Content .gitlab/generated/base-branch.txt -Raw).Trim()
if (-not $baseBranch.StartsWith('origin/')) { throw 'Expected an origin comparison branch from NUKE' }
$targetBranch = $baseBranch.Substring('origin/'.Length)
git check-ref-format "refs/heads/${targetBranch}"
if ($LASTEXITCODE -ne 0) { throw 'Invalid comparison branch from NUKE' }
git fetch --no-tags origin "+refs/heads/${targetBranch}:refs/remotes/origin/${targetBranch}"
if ($LASTEXITCODE -ne 0) { throw 'Failed to fetch the comparison branch' }
$changedFiles = @(git diff --name-only "${baseBranch}...HEAD")
if ($LASTEXITCODE -ne 0) { throw 'Failed to determine changed files' }

$changedFilesPath = Join-Path (Get-Location) '.gitlab/generated/changed-files.json'
[IO.File]::WriteAllText($changedFilesPath, (ConvertTo-Json -InputObject $changedFiles), (New-Object Text.UTF8Encoding $false))
$includeAll = $env:SHADOW_ALL_TEST_FRAMEWORKS -eq 'true'

& docker @dockerArguments `
    GenerateGitlabUnitTestsPipelines `
    --GitlabChangedFiles c:\mnt\.gitlab\generated\changed-files.json `
    @branchArguments `
    --IncludeAllTestFrameworks $includeAll
if ($LASTEXITCODE -ne 0) { throw "Unit-test matrix generation failed ($LASTEXITCODE)" }
