$ErrorActionPreference = 'Stop'

$hash = & tracer/build/_build/docker/gitlab/compute-image-hash.ps1
if ($LASTEXITCODE -ne 0 -or $hash -notmatch '^[0-9a-f]{12}$') {
    throw "compute-image-hash.ps1 did not produce a valid hash (exit=$LASTEXITCODE, output='$hash')"
}

$windowsBuildImage = "${env:WINDOWS_BUILD_IMAGE_BASE}:${hash}"
docker manifest inspect $windowsBuildImage | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Windows build image not found at $windowsBuildImage"
}

New-Item -ItemType Directory -Force artifacts/build_data/infra_logs | Out-Null

if (-not $env:DD_LOGGER_DD_API_KEY) {
    if (-not $env:DD_STS_OIDC_TOKEN) {
        throw 'DD_STS_OIDC_TOKEN is unavailable'
    }

    $response = Invoke-RestMethod `
        -Uri 'https://dd-sts.us1.ddbuild.io/sts/datadog/exchange?policy=apm-sdks-api-key' `
        -Headers @{ Authorization = "Bearer $env:DD_STS_OIDC_TOKEN" }
    if (-not $response.api_key) {
        throw 'The dd-sts response did not contain an API key'
    }

    $env:DD_LOGGER_DD_API_KEY = $response.api_key
    Write-Output 'CI Visibility API key configured using dd-sts'
}

$testSuite = if ($env:TEST_SUITE) { $env:TEST_SUITE } else { 'integration' }
$area = if ($env:AREA) { $env:AREA } else { $null }
$testFilter = $null

switch ($testSuite) {
    'integration' {
        # LocalDB, MSMQ, Chrome, IIS, and Docker dependencies are covered by
        # dedicated jobs or remain unavailable in the Windows build container.
        $testFilter = '(RunOnWindows=True)&(LoadFromGAC!=True)&(IIS!=True)&(IISExpress!=True)&(Category!=AzureFunctions)&(SkipInCI!=True)&(RequiresDockerDependency!=true)&(RequiresLocalDb!=True)&(RequiresMsmq!=True)&(RequiresChrome!=True)'
        $nukeTargets = 'CompileTrimmingSamples BuildIntegrationTests RunIntegrationTests'
        $nukeArguments = '--IncludeTestsRequiringDocker false'
    }
    'iis' {
        $nukeTargets = 'BuildAspNetIntegrationTests RunWindowsTracerIisIntegrationTests'
        $nukeArguments = ''
    }
    'debugger' {
        $optimize = if ($env:OPTIMIZE) { $env:OPTIMIZE } else { 'true' }
        $nukeTargets = 'BuildDebuggerIntegrationTests RunDebuggerIntegrationTests'
        $nukeArguments = "--DebugType portable --Optimize $optimize"
    }
    default {
        throw "Unknown Windows integration test suite '$testSuite'"
    }
}

$commonDockerArguments = @(
    '--rm',
    '-m', '20480M',
    '-v', "$(Get-Location):c:\mnt",
    '-e', 'CI_JOB_ID',
    '-e', 'CI=true',
    '-e', 'WINDOWS_BUILDER=true',
    '-e', 'AWS_NETWORKING=true',
    '-e', 'NUGET_CERT_REVOCATION_MODE=offline',
    '-e', 'NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true',
    '-e', 'DD_LOGGER_ENABLED=true',
    '-e', 'DD_LOGGER_DD_API_KEY',
    '-e', 'DD_LOGGER_DD_SERVICE=dd-trace-dotnet',
    '-e', 'DD_LOGGER_DD_TRACE_LOG_PATH=c:\mnt\artifacts\build_data\infra_logs\integration-ci-visibility.log',
    '-e', "DD_LOGGER_DD_TAGS=test.configuration.job:$env:CI_JOB_NAME",
    '-e', 'IncludeTestsRequiringDocker=false',
    '-e', 'IncludeAllTestFrameworks=true',
    '-e', 'TargetPlatform=x64',
    '-e', 'enable_crash_dumps=true',
    '-e', "SourceRevisionId=$env:CI_COMMIT_SHA",
    '-e', 'RepositoryUrl=https://github.com/DataDog/dd-trace-dotnet.git',
    '-e', 'GITLAB_CI',
    '-e', 'CI_PROJECT_URL',
    '-e', 'CI_PIPELINE_ID',
    '-e', 'CI_REPOSITORY_URL',
    '-e', 'CI_COMMIT_SHA',
    '-e', 'CI_COMMIT_BRANCH',
    '-e', 'CI_COMMIT_TAG',
    '-e', 'CI_COMMIT_REF_NAME',
    '-e', 'CI_PROJECT_DIR=c:\mnt',
    '-e', 'CI_PROJECT_PATH',
    '-e', 'CI_PROJECT_NAME',
    '-e', 'CI_PIPELINE_IID',
    '-e', 'CI_PIPELINE_URL',
    '-e', 'CI_JOB_URL',
    '-e', 'CI_JOB_NAME',
    '-e', 'CI_JOB_NAME_SLUG',
    '-e', 'CI_JOB_STAGE',
    '-e', 'CI_COMMIT_MESSAGE',
    '-e', 'CI_COMMIT_AUTHOR',
    '-e', 'CI_COMMIT_TIMESTAMP',
    '-e', 'CI_RUNNER_ID',
    '-e', 'CI_RUNNER_TAGS',
    '-e', 'CI_MERGE_REQUEST_SOURCE_BRANCH_SHA',
    '-e', 'CI_MERGE_REQUEST_TARGET_BRANCH_SHA',
    '-e', 'CI_MERGE_REQUEST_DIFF_BASE_SHA',
    '-e', 'CI_MERGE_REQUEST_TARGET_BRANCH_NAME',
    '-e', 'CI_MERGE_REQUEST_IID'
)

if ($testFilter) {
    $commonDockerArguments += @('-e', "Filter=$testFilter")
}

if ($area) {
    $commonDockerArguments += @('-e', "Area=$area")
}

Write-Output "Building and running Windows x64 $testSuite tests for $env:FRAMEWORK (area=$area)"
$testCommand = "reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f && powershell -NoProfile -ExecutionPolicy Bypass -File c:\mnt\.gitlab\install-windows-test-runtime.ps1 -Framework $env:FRAMEWORK -IncludeAspNetCore && c:\entrypoint.bat $nukeTargets --framework $env:FRAMEWORK --TargetPlatform x64 --IncludeAllTestFrameworks true $nukeArguments --NugetPackageDirectory c:\mnt\packages"

& docker run @commonDockerArguments --entrypoint cmd.exe $windowsBuildImage /d /s /c $testCommand
$testExitCode = $LASTEXITCODE

& docker run @commonDockerArguments $windowsBuildImage CheckBuildLogsForErrors --NugetPackageDirectory c:\mnt\packages
$logCheckExitCode = $LASTEXITCODE

if ($testExitCode -ne 0) {
    throw "Windows $testSuite tests for $env:FRAMEWORK exited with code $testExitCode"
}

if ($logCheckExitCode -ne 0) {
    throw "Build-log validation exited with code $logCheckExitCode"
}
