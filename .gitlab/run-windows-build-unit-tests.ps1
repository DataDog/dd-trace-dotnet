$ErrorActionPreference = 'Stop'
$containerEnvironment = @(
    'CI_COMMIT_SHA',
    'CI_COMMIT_BRANCH',
    'ENABLE_MULTIPROCESSOR_COMPILATION=true',
    'CL_MPCount=16'
)

# Keep shadow outputs separate from the existing signed build/publication workflow.
& "$PSScriptRoot/run-windows-container.ps1" -Environment $containerEnvironment -Command @(
    'Info', 'Clean', 'BuildTracerHome', 'BuildProfilerHome', 'BuildNativeLoader', 'BuildDdDotnet',
    'PublishOpenTelemetryStartupHook', 'BuildManagedUnitTests', '--NugetPackageDirectory', 'c:\mnt\packages'
)
if ($LASTEXITCODE -ne 0) { throw "Windows shadow build failed ($LASTEXITCODE)" }

& "$PSScriptRoot/run-windows-container.ps1" -Environment $containerEnvironment -Command @(
    'CompileTracerNativeTests', 'RunTracerNativeTests', 'CompileNativeLoaderNativeTests', 'RunNativeLoaderNativeTests',
    'CompileProfilerNativeTests', 'RunProfilerNativeTests', '--NugetPackageDirectory', 'c:\mnt\packages'
)
if ($LASTEXITCODE -ne 0) { throw "Windows native unit tests failed ($LASTEXITCODE)" }
