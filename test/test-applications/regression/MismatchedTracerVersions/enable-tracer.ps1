Param (
    [Parameter(Mandatory)]
    [ValidateScript( { Test-Path $_ -PathType ‘Container’ })]
    [String]
    $TracerHome,

    [String]
    $LogPath
)

$ErrorActionPreference = 'Stop'

$TracerGuid = '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
$TracerPath32 = ''
$TracerPath64 = ''
$TracerHome = Resolve-Path $TracerHome
$JsonPath = Join-Path -Path $TracerHome -ChildPath 'integrations.json'

if ($LogPath -eq "") {
    $LogPath = Join-Path -Path $TracerHome -ChildPath 'logs'
}

if ($env:os -eq 'Windows_NT') {
    Write-Output 'Setting environment variables for Windows'
    $TracerPath32 = Join-Path -Path $TracerHome -ChildPath 'win-x86\Datadog.Trace.ClrProfiler.Native.dll'
    $TracerPath64 = Join-Path -Path $TracerHome -ChildPath 'win-x64\Datadog.Trace.ClrProfiler.Native.dll'
}
else {
    Write-Verbose 'Setting environment variables for Linux'
    $TracerPath64 = Join-Path -Path $TracerHome -ChildPath 'Datadog.Trace.ClrProfiler.Native.so'
}

$null = New-Item -Path $LogPath -ItemType Directory -Force

function Set-EnvironmentVariable {
    param([String] $name, [String] $value)

    Write-Verbose "$name=$value"
    Set-Item -Path "Env:$name" -Value $value
}

# Set the environment variables to attach the tracer
Set-EnvironmentVariable 'DD_DOTNET_TRACER_HOME' $TracerHome
Set-EnvironmentVariable 'DD_INTEGRATIONS' $JsonPath
Set-EnvironmentVariable 'DD_TRACE_LOG_DIRECTORY' $LogPath

if ($env:os -eq 'Windows_NT') {
    Set-EnvironmentVariable 'DD_PROFILER_EXCLUDE_PROCESSES' 'dotnet.exe;devenv.exe;Microsoft.ServiceHub.Controller.exe;ServiceHub.Host.CLR.exe;ServiceHub.TestWindowStoreHost.exe;ServiceHub.DataWarehouseHost.exe;sqlservr.exe;VBCSCompiler.exe;iisexpresstray.exe;msvsmon.exe;PerfWatson2.exe;ServiceHub.IdentityHost.exe;ServiceHub.VSDetouredHost.exe;ServiceHub.SettingsHost.exe;ServiceHub.Host.CLR.x86.exe;vstest.console.exe;ServiceHub.RoslynCodeAnalysisService32.exe;testhost.x86.exe;MSBuild.exe;ServiceHub.ThreadedWaitDialog.exe;OmniSharp.exe;CodeHelper.exe;pwsh.exe'
}

Set-EnvironmentVariable 'CORECLR_ENABLE_PROFILING' '1'
Set-EnvironmentVariable 'CORECLR_PROFILER' $TracerGuid
Set-EnvironmentVariable 'CORECLR_PROFILER_PATH_32' $TracerPath32
Set-EnvironmentVariable 'CORECLR_PROFILER_PATH_64' $TracerPath64

Set-EnvironmentVariable 'COR_ENABLE_PROFILING' '1'
Set-EnvironmentVariable 'COR_PROFILER' $TracerGuid
Set-EnvironmentVariable 'COR_PROFILER_PATH_32' $TracerPath32
Set-EnvironmentVariable 'COR_PROFILER_PATH_64' $TracerPath64
