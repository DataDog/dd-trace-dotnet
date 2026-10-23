$ErrorActionPreference = 'Stop'
$hash = & tracer/build/_build/docker/gitlab/compute-image-hash.ps1
if ($LASTEXITCODE -ne 0 -or $hash -notmatch '^[0-9a-f]{12}$') {
    throw 'Unable to compute the Windows build image hash'
}
$image = "${env:WINDOWS_BUILD_IMAGE_BASE}:${hash}"
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
