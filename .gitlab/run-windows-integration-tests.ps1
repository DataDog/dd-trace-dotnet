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
$targetPlatform = if ($env:TARGET_PLATFORM) { $env:TARGET_PLATFORM } else { 'x64' }
if ($targetPlatform -notin @('x64', 'x86')) {
    throw "Unsupported Windows target platform '$targetPlatform'"
}

$testFilter = $null

switch ($testSuite) {
    'integration' {
        # LocalDB, IIS, Chrome, and Docker dependencies are covered by dedicated jobs.
        # MSMQ is available in the Windows build image.
        $testFilter = '(RunOnWindows=True)&(LoadFromGAC!=True)&(IIS!=True)&(IISExpress!=True)&(Category!=AzureFunctions)&(SkipInCI!=True)&(RequiresDockerDependency!=true)&(RequiresLocalDb!=True)&(RequiresChrome!=True)'
        if ($area -eq 'ASM') {
            # These ASM tests do not declare their LocalDB/IIS requirements as
            # traits, so keep them out of the dependency-free Windows slice.
            $testFilter += '&(FullyQualifiedName!~IIS)&(FullyQualifiedName!=Datadog.Trace.Security.IntegrationTests.Iast.IastInstrumentationUnitTests.TestInstrumentedUnitTests)'
        }
        $nukeTargets = 'CompileTrimmingSamples BuildIntegrationTests RunIntegrationTests'
        $nukeArguments = '--IncludeTestsRequiringDocker false'
    }
    'iis' {
        # The ASP.NET projects use packages.config. Restore them explicitly because
        # GitLab jobs do not download Azure's pre-restored working-directory artifact.
        $nukeTargets = 'Restore BuildAspNetIntegrationTests RunWindowsTracerIisIntegrationTests'
        $nukeArguments = ''
    }
    'localdb' {
        $testFilter = '(RunOnWindows=True)&(RequiresLocalDb=True)&(SkipInCI!=True)'
        $nukeTargets = 'CompileTrimmingSamples BuildIntegrationTests RunIntegrationTests'
        $nukeArguments = '--IncludeTestsRequiringDocker false'
    }
    'selenium' {
        $testFilter = '(RunOnWindows=True)&(RequiresChrome=True)&(SkipInCI!=True)&(RequiresDockerDependency!=true)'
        # The Selenium sample is supplied by build-samples-standalone, so the
        # trimming-sample build is not required for this focused job.
        $nukeTargets = 'BuildIntegrationTests'
        $nukeArguments = '--IncludeTestsRequiringDocker false'
    }
    'azure-functions' {
        if ($targetPlatform -ne 'x64' -or $env:FRAMEWORK -notin @('net6.0', 'net7.0', 'net8.0', 'net9.0', 'net10.0')) {
            throw "The Windows Azure Functions suite does not support $targetPlatform $env:FRAMEWORK"
        }

        $testFilter = '(RunOnWindows=True)&(Category=AzureFunctions)&(SkipInCI!=True)'
        $nukeTargets = 'BuildAndRunWindowsAzureFunctionsTests'
        $nukeArguments = ''
    }
    'debugger' {
        $optimize = if ($env:OPTIMIZE) { $env:OPTIMIZE } else { 'true' }
        $debugType = if ($env:DEBUG_TYPE) { $env:DEBUG_TYPE } else { 'portable' }
        if ($debugType -notin @('portable', 'full')) {
            throw "Unsupported debugger PDB type '$debugType'"
        }

        $nukeTargets = 'BuildDebuggerIntegrationTests RunDebuggerIntegrationTests'
        $nukeArguments = "--DebugType $debugType --Optimize $optimize"
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
    '-e', "TargetPlatform=$targetPlatform",
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

if ($testSuite -eq 'selenium') {
    if ($env:FRAMEWORK -ne 'net10.0' -or $targetPlatform -ne 'x64') {
        throw 'The host Selenium job currently supports only net10.0 on x64'
    }

    $repositoryRoot = (Get-Location).Path
    $dotnetRoot = Join-Path $repositoryRoot '.dotnet'
    $dotnetCliHome = Join-Path $repositoryRoot '.dotnet_cli_home'
    $toolRoot = Join-Path $repositoryRoot "artifacts\build_data\selenium-tools-$env:CI_JOB_ID"
    $chromeRoot = Join-Path $toolRoot 'chrome'
    $chromeDriverRoot = Join-Path $toolRoot 'chromedriver'
    $seleniumTempRoot = Join-Path $toolRoot 'temp'
    $seleniumLogRoot = Join-Path $repositoryRoot 'artifacts\build_data\infra_logs\selenium'
    $packagesRoot = Join-Path $repositoryRoot 'packages'
    $toolContainer = $null

    New-Item -ItemType Directory -Force $dotnetRoot, $dotnetCliHome, $chromeRoot, $chromeDriverRoot, $seleniumTempRoot, $seleniumLogRoot, $packagesRoot | Out-Null

    # Keep the SDK and all CLI state in the checkout because Windows runners are
    # shared and persistent. The installation script is also used by container jobs.
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_CLI_HOME = $dotnetCliHome
    $env:NUGET_PACKAGES = $packagesRoot
    $env:PATH = "$dotnetRoot;$env:PATH"

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File .gitlab/install-windows-test-runtime.ps1 `
        -Framework $env:FRAMEWORK `
        -Architecture $targetPlatform `
        -InstallDir $dotnetRoot `
        -IncludeAspNetCore `
        -InstallSdk
    if ($LASTEXITCODE -ne 0) {
        throw "Checkout-local .NET SDK installation exited with code $LASTEXITCODE"
    }

    try {
        # Build in the image, which contains the established NuGet credentials and
        # toolchain. Only the already-built test process runs on the full host OS.
        $buildCommand = "reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f && powershell -NoProfile -ExecutionPolicy Bypass -File c:\mnt\.gitlab\install-windows-test-runtime.ps1 -Framework $env:FRAMEWORK -Architecture $targetPlatform -IncludeAspNetCore && c:\entrypoint.bat $nukeTargets --framework $env:FRAMEWORK --TargetPlatform $targetPlatform --IncludeAllTestFrameworks true $nukeArguments --NugetPackageDirectory c:\mnt\packages"
        & docker run @commonDockerArguments --entrypoint cmd.exe $windowsBuildImage /d /s /c $buildCommand
        $buildExitCode = $LASTEXITCODE
        if ($buildExitCode -ne 0) {
            # Preserve the usual validation diagnostics even when compilation fails.
            & docker run @commonDockerArguments $windowsBuildImage CheckBuildLogsForErrors --NugetPackageDirectory c:\mnt\packages
            throw "Windows $testSuite build for $env:FRAMEWORK exited with code $buildExitCode"
        }

        # Reuse the exact, checksum-verified Chrome and ChromeDriver versions from
        # the content-addressed build image without trying to run Chrome in Server Core.
        $toolContainer = (& docker create --entrypoint cmd.exe $windowsBuildImage /d /c exit 0 | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or -not $toolContainer) {
            throw "Could not create a temporary container from $windowsBuildImage"
        }

        & docker cp "${toolContainer}:C:\devtools\chrome\chrome-headless-shell-win64\." $chromeRoot
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not copy Chrome from the Windows build image'
        }

        & docker cp "${toolContainer}:C:\devtools\chromedriver\chromedriver-win64\." $chromeDriverRoot
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not copy ChromeDriver from the Windows build image'
        }

        $chromeExecutable = Join-Path $chromeRoot 'chrome-headless-shell.exe'
        $chromeDriverExecutable = Join-Path $chromeDriverRoot 'chromedriver.exe'
        if (-not (Test-Path -LiteralPath $chromeExecutable -PathType Leaf)) {
            throw "Chrome was not copied to '$chromeExecutable'"
        }

        if (-not (Test-Path -LiteralPath $chromeDriverExecutable -PathType Leaf)) {
            throw "ChromeDriver was not copied to '$chromeDriverExecutable'"
        }

        $env:CI = 'true'
        $env:WINDOWS_BUILDER = 'true'
        $env:NUGET_CERT_REVOCATION_MODE = 'offline'
        $env:NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY = 'true'
        $env:SAMPLES_SELENIUM_CHROME_BINARY = $chromeExecutable
        $env:SAMPLES_SELENIUM_CHROMEDRIVER_DIRECTORY = $chromeDriverRoot
        $env:SAMPLES_SELENIUM_HEADLESS = 'true'
        $env:SAMPLES_SELENIUM_LOG_DIRECTORY = $seleniumLogRoot
        $env:TEMP = $seleniumTempRoot
        $env:TMP = $seleniumTempRoot
        $env:DD_LOGGER_ENABLED = 'true'
        $env:DD_LOGGER_DD_SERVICE = 'dd-trace-dotnet'
        $env:DD_LOGGER_DD_TRACE_LOG_PATH = Join-Path $repositoryRoot 'artifacts\build_data\infra_logs\integration-ci-visibility.log'
        $env:DD_LOGGER_DD_TAGS = "test.configuration.job:$env:CI_JOB_NAME"
        $env:IncludeTestsRequiringDocker = 'false'
        $env:IncludeAllTestFrameworks = 'true'
        $env:TargetPlatform = 'X64'
        $env:enable_crash_dumps = 'true'
        $env:SourceRevisionId = $env:CI_COMMIT_SHA
        $env:RepositoryUrl = 'https://github.com/DataDog/dd-trace-dotnet.git'
        $env:Filter = $testFilter
        $env:Area = $area
        $env:MonitoringHomeDirectory = Join-Path $repositoryRoot 'artifacts\monitoring-home'
        $env:USE_FULL_TEST_CONFIG = 'True'
        $env:DD_TRACE_LOG_DIRECTORY = Join-Path $repositoryRoot 'artifacts\build_data\logs'
        $env:DD_LOGGER_BUILD_SOURCESDIRECTORY = $repositoryRoot
        $env:DD_CIVISIBILITY_CODE_COVERAGE_SNK_FILEPATH = Join-Path $repositoryRoot 'Datadog.Trace.snk'
        $env:COMPlus_DbgEnableMiniDump = '1'
        $env:COMPlus_DbgMiniDumpType = '2'
        $env:COMPlus_EnableCrashReport = '1'

        $effectiveFilter = $testFilter
        if ($area) {
            $effectiveFilter = "($effectiveFilter)&(Area=$area)"
        }

        $testProject = Join-Path $repositoryRoot 'tracer\test\Datadog.Trace.ClrProfiler.IntegrationTests\Datadog.Trace.ClrProfiler.IntegrationTests.csproj'
        $resultsDirectory = Join-Path $repositoryRoot 'artifacts\build_data\results\Datadog.Trace.ClrProfiler.IntegrationTests'
        New-Item -ItemType Directory -Force $resultsDirectory, $env:DD_TRACE_LOG_DIRECTORY | Out-Null

        Write-Output "Running Windows $targetPlatform $testSuite tests directly on the runner for $env:FRAMEWORK (area=$area)"
        $dotnetExecutable = Join-Path $dotnetRoot 'dotnet.exe'
        & $dotnetExecutable test $testProject `
            --configuration Release `
            --framework $env:FRAMEWORK `
            --filter $effectiveFilter `
            --logger trx `
            --no-build `
            --no-restore `
            --results-directory $resultsDirectory `
            --settings (Join-Path $repositoryRoot 'tracer\test\test.settings') `
            /property:Platform=AnyCPU
        $testExitCode = $LASTEXITCODE

        & docker run @commonDockerArguments $windowsBuildImage CheckBuildLogsForErrors --NugetPackageDirectory c:\mnt\packages
        $logCheckExitCode = $LASTEXITCODE

        if ($testExitCode -ne 0) {
            throw "Windows $testSuite tests for $env:FRAMEWORK exited with code $testExitCode"
        }

        if ($logCheckExitCode -ne 0) {
            throw "Build-log validation exited with code $logCheckExitCode"
        }
    }
    finally {
        if ($toolContainer) {
            & docker rm --force $toolContainer | Out-Null
        }

        Remove-Item -LiteralPath $toolRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    return
}

Write-Output "Building and running Windows $targetPlatform $testSuite tests for $env:FRAMEWORK (area=$area)"
$dependencySetup = if ($testSuite -eq 'localdb') {
    'powershell -NoProfile -ExecutionPolicy Bypass -File c:\mnt\.gitlab\initialize-localdb.ps1 && '
} elseif ($testSuite -eq 'azure-functions') {
    'set "PATH=C:\Program Files\Microsoft\Azure Functions Core Tools;C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator;%PATH%" && powershell -NoProfile -ExecutionPolicy Bypass -File c:\mnt\.gitlab\initialize-azure-functions.ps1 && '
} elseif ($testSuite -eq 'integration' -and $area -eq 'Tracer' -and $env:FRAMEWORK -eq 'net48') {
    'powershell -NoProfile -ExecutionPolicy Bypass -File c:\mnt\.gitlab\initialize-msmq.ps1 && '
} else {
    ''
}
$testCommand = "reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f && powershell -NoProfile -ExecutionPolicy Bypass -File c:\mnt\.gitlab\install-windows-test-runtime.ps1 -Framework $env:FRAMEWORK -Architecture $targetPlatform -IncludeAspNetCore && $dependencySetup" + "c:\entrypoint.bat $nukeTargets --framework $env:FRAMEWORK --TargetPlatform $targetPlatform --IncludeAllTestFrameworks true $nukeArguments --NugetPackageDirectory c:\mnt\packages"

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
