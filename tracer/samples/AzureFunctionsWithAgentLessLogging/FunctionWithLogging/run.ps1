<#
.SYNOPSIS
    A script that allows the user to run C# sample applications to demonstrate
    the setup and usage of Datadog's .NET Tracer log collection and log trace ID injection in a function
.PARAMETER AppDirectory
    The application directory - defaults to the location of this script and need not be set
    by the user unless samples are moved around.
.PARAMETER ApiKey
    The Datadog API key
.EXAMPLE
    pwsh BuildAndRunSample.ps1 -ApiKey YOUR_API_KEY_HERE
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [System.String]
    $AppDirectory, # only necessary if the script is removed from its current directory layout

    [Parameter(Mandatory = $false)]
    [System.String]
    $ApiKey
)

<#
.DESCRIPTION
    Checks to ensure that the dotnet command line too is accessible.
#>
function Test-DotNetCLI {
    try {
        dotnet --version
        return $true
    }
    catch {
        return $false # dotnet CLI not installed
    }
}

function Set-EnvironmentVariableForProcess {
    param([string]$name, [string]$value)
    Write-Host "Setting $name to $value"
    [System.Environment]::SetEnvironmentVariable($name, $value, [System.EnvironmentVariableTarget]::Process)
}

<#
.DESCRIPTION
    Builds and runs the desired sample project.
#>
function BuildAndRunSample {
    func start
}


if (-not (Test-DotNetCLI)) {
    Write-Error "The dotnet CLI tool wasn't found - please ensure it is installed."
    exit 1
}

## Configure logs injection by setting up Unified Service Tagging
## cf https://docs.datadoghq.com/tracing/other_telemetry/connect_logs_and_traces/dotnet/?tab=serilog
Set-EnvironmentVariableForProcess -name "DD_LOGS_INJECTION" -value "true"
Set-EnvironmentVariableForProcess -name "DD_ENV" -value "dev"
Set-EnvironmentVariableForProcess -name "DD_SERVICE" -value "LogsInjectionInFunctionSamples"
Set-EnvironmentVariableForProcess -name "DD_VERSION" -value "1.0.0"
Set-EnvironmentVariableForProcess -name "DD_API_KEY" -value $ApiKey
Set-EnvironmentVariableForProcess -name "DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS" -value "ILogger"

if ($IsWindows -or ([System.Environment]::OSVersion.Platform -eq "Win32NT")) {
    Write-Host "Using Windows configuration"
    if ($AppDirectory -eq $null)
    {
        $AppDirectory = "$pwd"
    }
    $DotNetTracerHome = Join-Path $AppDirectory "datadog"

    $ClrProfilerPath = ""
    if ($Runtime -eq "x86") {
        $ClrProfilerPath = Join-Path $DotNetTracerHome "win-x86\Datadog.Trace.ClrProfiler.Native.dll"
    }
    else {
        $ClrProfilerPath = Join-Path $DotNetTracerHome "win-x64\Datadog.Trace.ClrProfiler.Native.dll"
    }

    if ($Framework -eq "net462") {
        # Use the .NET Framework specific variables
        Set-EnvironmentVariableForProcess -name "COR_ENABLE_PROFILING" -value "1"
        Set-EnvironmentVariableForProcess -name "COR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
        Set-EnvironmentVariableForProcess -name "COR_PROFILER_PATH" -value $ClrProfilerPath
    }
    else {
        # Use the .NET Core specific variables
        Set-EnvironmentVariableForProcess -name "CORECLR_ENABLE_PROFILING" -value "1"
        Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
        Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER_PATH" -value $ClrProfilerPath
    }

    # dotnet tracer home example: "..\Log4NetExample\bing\Debug\net7.0\datadog"
    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}

if ($IsLinux) {
    Write-Host "Using Linux configuration"

    $DotNetTracerHome = Join-PAth $AppDirectory "datadog"
    $ClrProfilerPath = Join-Path $DotNetTracerHome "/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
    # if Alpine Linux x64
    # $ClrProfilerPath = Join-Path $AppDirectory "/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so"
    # if Linux ARM64
    # $ClrProfilerPath = Join-Path $AppDirectory "/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so"

    # Depending on 32-bit vs 64-bit the path to the profiler will change
    Set-EnvironmentVariableForProcess -name "CORECLR_ENABLE_PROFILING" -value "1"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER_PATH" -value $ClrProfilerPath

    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}

if ($IsMacOs) {
    Write-Host "Using OsX configuration"
    $DotNetTracerHome = Join-PAth $AppDirectory "datadog"
    $ClrProfilerPath = Join-Path $DotNetTracerHome "/osx/Datadog.Trace.ClrProfiler.Native.dylib"

    # Depending on 32-bit vs 64-bit the path to the profiler will change
    Set-EnvironmentVariableForProcess -name "CORECLR_ENABLE_PROFILING" -value "1"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER_PATH" -value $ClrProfilerPath

    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}