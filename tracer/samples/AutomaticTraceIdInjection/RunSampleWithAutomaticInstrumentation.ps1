param(
    [Parameter(Mandatory = $false)]
    [System.String]
    [ValidateSet("Log4Net", "MicrosoftExtensions", "NLog40", "NLog45", "NLog46", "Serilog")]
    $LoggingLibrary = "Log4Net",

    [Parameter(Mandatory = $false)]
    [System.String]
    [ValidateSet("net7.0", "netcoreapp2.1", "net45", "netcoreapp3.1")]
    $Framework = "net7.0",

    [Parameter(Mandatory = $false)]
    [System.String]
    $AppDirectory
)

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

function BuildAndRunSample {
    $Project = Join-Path $PSScriptRoot $LoggingLibrary"Example"

    Write-Host "Project path is $Project"
    dotnet build --configuration Debug $Project
    dotnet run --project $Project
}

function Get-ApplicationDirectory {
    if ([System.String]::IsNullOrWhiteSpace($AppDirectory)) {
        return Join-Path $PSScriptRoot $LoggingLibrary"Example" "bin\Debug\" $Framework
    }

    Write-Host "Application directory has been set to: $AppDirectory"
    return $AppDirectory
}

if (-not (Test-DotNetCLI)) {
    Write-Error "The dotnet CLI tool wasn't found - please ensure it is installed."
    exit 1
}

Write-Host "Using $LoggingLibrary sample with target framework: $Framework"

## Configure logs injection by setting up Unified Service Tagging
## cf https://docs.datadoghq.com/tracing/other_telemetry/connect_logs_and_traces/dotnet/?tab=serilog
Set-EnvironmentVariableForProcess -name "DD_LOGS_INJECTION" -value "true"
Set-EnvironmentVariableForProcess -name "DD_ENV" -value "steven.bouwkamp"
Set-EnvironmentVariableForProcess -name "DD_SERVICE" -value "LogsInjectiongSamples"
Set-EnvironmentVariableForProcess -name "DD_VERSION" -value "1.0.0"

# If you are running into issues, uncomment the below to enable debug logs
# Set-EnvironmentVariableForProcess -name "DD_TRACE_DEBUG" -value "true"

## If you want to enable Agentless logging uncomment THESE 2 lines and set your api key
## If you are not using Agentless logging, the agent must be configured to retrieve your logs
# Set-EnvironmentVariableForProcess -name "DD_API_KEY" -value "YOUR_API_KEY"
# Set-EnvironmentVariableForProcess -name "D_LOGS_DIRECT_SUBMISSION_INTEGRATIONS" -value "LOGGING_LIBRARY_HERE"

if ($IsWindows) {
    Write-Host "Using Windows configuration"
    $AppDirectory = Get-ApplicationDirectory

    # Configure the tracer
    # For reference: https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/Datadog.Trace.Bundle/README.md#configure-the-tracer

    if ($Framework -like "net4*") {
        # Use the .NET Framework specific variables
        Set-EnvironmentVariableForProcess -name "COR_ENABLE_PROFILING" -value "1"
        Set-EnvironmentVariableForProcess -name "COR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
        $ClrPath = Join-Path $AppDirectory "datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
        Set-EnvironmentVariableForProcess -name "COR_PROFILER_PATH" -value $ClrPath
    }
    else {
        # Use the .NET Core specific variables
        Set-EnvironmentVariableForProcess -name "CORECLR_ENABLE_PROFILING" -value "1"
        Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
        $CoreClrPath = Join-Path $AppDirectory "datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
        Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER_PATH" -value $CoreClrPath
    }
    
    # dotnet tracer home example: "..\Log4NetExample\bing\Debug\net7.0\datadog"
    $DotNetTracerHome = Join-Path $AppDirectory "datadog"
    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}

if ($IsLinux) {
    Write-Host "Using Linux configuration"
    $AppDirectory = Get-ApplicationDirectory

    # Depending on 32-bit vs 64-bit the path to the profiler will change
    Set-EnvironmentVariableForProcess -name "CORECLR_ENABLE_PROFILING" -value "1"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    $CoreClrPath = Join-Path $AppDirectory "/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
    # if Alpine Linux x64
    # $CoreClrPath = Join-Path $AppDirectory "/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so"
    # if Linux ARM64
    # $CoreClrPath = Join-Path $AppDirectory "/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER_PATH" -value $CoreClrPath

    $DotNetTracerHome = Join-PAth $AppDirectory "datadog"
    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}
