$ErrorActionPreference = 'Stop'
$image = & "$PSScriptRoot/get-windows-build-image.ps1"
$dockerArguments = @(
    'run', '--rm', '-m', '20480M',
    '-v', "$(Get-Location):c:\mnt",
    '-e', 'CI_JOB_ID',
    '-e', 'CI_PROJECT_NAME',
    '-e', 'CI_JOB_NAME_SLUG',
    '-e', 'CI_COMMIT_SHA',
    '-e', 'CI_COMMIT_BRANCH',
    '-e', 'ENABLE_MULTIPROCESSOR_COMPILATION=true',
    '-e', 'CL_MPCount=16',
    '-e', 'WINDOWS_BUILDER=true',
    '-e', 'AWS_NETWORKING=true',
    '-e', 'NUGET_CERT_REVOCATION_MODE=offline',
    '-e', 'CI_IDENTITIES_GITLAB_ID_TOKEN',
    $image
)

# Keep shadow outputs separate from the existing signed build/publication workflow.
& docker @dockerArguments Info Clean BuildTracerHome BuildProfilerHome BuildNativeLoader BuildDdDotnet PublishOpenTelemetryStartupHook `
    --NugetPackageDirectory c:\mnt\packages
if ($LASTEXITCODE -ne 0) { throw "Windows shadow build failed ($LASTEXITCODE)" }

& docker @dockerArguments CompileTracerNativeTests RunTracerNativeTests `
    CompileNativeLoaderNativeTests RunNativeLoaderNativeTests `
    CompileProfilerNativeTests RunProfilerNativeTests `
    --NugetPackageDirectory c:\mnt\packages
if ($LASTEXITCODE -ne 0) { throw "Windows native unit tests failed ($LASTEXITCODE)" }
