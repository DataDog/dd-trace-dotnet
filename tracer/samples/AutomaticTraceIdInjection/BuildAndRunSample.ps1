<#
.SYNOPSIS
    A script that allows the user to run C# sample applications to demonstrate
    the setup and usage of Datadog's .NET Tracer log collection and log trace ID injection.
.PARAMETER LoggingLibrary
    What sample to run based on the logging library: "Log4Net", "Serilog", "NLog40", "NLog45", "NLog46", or "MicrosoftExtensions"
.PARAMETER Framework
    What .NET Framework should be used to build/run - either "net462" or "net7.0"
.PARAMETER Runtime
    What runtime should be built/run - either "x86" or "x64".
.PARAMETER AppDirectory
    The application directory - defaults to the location of this script and need not be set
    by the user unless samples are moved around.
.PARAMETER Agentless
    Switch to enable agentless logging instead of file-tail logging
.PARAMETER ApiKey
    The Datadog API key (only required when -Agentless is set)
.PARAMETER EnableDebug
    Switch to enable debug mode to get .NET Tracer's debug logs.
.EXAMPLE
    pwsh BuildAndRunSample.ps1 -LoggingLibrary Serilog -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [System.String]
    [ValidateSet("Log4Net", "MicrosoftExtensions", "NLog40", "NLog45", "NLog46", "Serilog")]
    $LoggingLibrary = "Log4Net",

    [Parameter(Mandatory = $false)]
    [System.String]
    [ValidateSet("net7.0", "net462")]
    $Framework = "net462",

    [Parameter(Mandatory = $false)]
    [System.String]
    [ValidateSet("x86", "x64")]
    $Runtime = "x86",

    [Parameter(Mandatory = $false)]
    [System.String]
    $AppDirectory, # only necessary if the script is removed from its current directory layout

    [Parameter(Mandatory = $false)]
    [switch]
    $Agentless,

    [Parameter(Mandatory = $false)]
    [System.String]
    $ApiKey,

    [Parameter(Mandatory = $false)]
    [switch]
    $EnableDebug
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

<#
.DESCRIPTION
    Sets an environment variable to the specified value for the process.
#>
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
    $Project = Join-Path $PSScriptRoot $LoggingLibrary"Example"

    Write-Host "Project path is $Project"
    dotnet restore $Project
    dotnet build --configuration Debug $Project --framework $Framework
    dotnet run --project $Project --framework $Framework --arch $Runtime
}

<#
.DESCRIPTION
    If necessary, sets the application directory that will be used to find the datadog profiler.
#>
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
Set-EnvironmentVariableForProcess -name "DD_ENV" -value "dev"
Set-EnvironmentVariableForProcess -name "DD_SERVICE" -value "LogsInjectionSamples"
Set-EnvironmentVariableForProcess -name "DD_VERSION" -value "1.0.0"

# If you are running into issues, pass in the -EnableDebug switch to get Debug Logs

if ($EnableDebug) {
    Set-EnvironmentVariableForProcess -name "DD_TRACE_DEBUG" -value "true"
}

## If you want to enable Agentless logging uncomment THESE 2 lines and set your api key
## If you are not using Agentless logging, the agent must be configured to retrieve your logs
if ($Agentless) {
    if ($ApiKey -eq "") {
        Write-Error "For enabling Agentless logging please supply your API key with the -ApiKey parameter"
        return 1
    }
    Set-EnvironmentVariableForProcess -name "DD_API_KEY" -value $ApiKey

    if ($LoggingLibrary -like "NLog*"){
        Set-EnvironmentVariableForProcess -name "DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS" -value "NLog"
    }
    elseif ($LoggingLibrary -eq "MicrosoftExtensions") {
        Set-EnvironmentVariableForProcess -name "DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS" -value "ILogger"
    }
    else {
        Set-EnvironmentVariableForProcess -name "DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS" -value $LoggingLibrary
    }
}

# Set-EnvironmentVariableForProcess -name "DD_SITE" -value "datadoghq.com"

if ($IsWindows) {
    Write-Host "Using Windows configuration"
    $AppDirectory = Get-ApplicationDirectory

    # Depending on the OS and process architecture, the path to the profiler will vary
    # To see the possible paths: https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/Datadog.Trace.Bundle/README.md#configure-the-tracer
    $ClrProfilerPath = ""
    if ($Runtime -eq "x86") {
        $ClrProfilerPath = Join-Path $AppDirectory "datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll"
    }
    else {
        $ClrProfilerPath = Join-Path $AppDirectory "datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
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
    $DotNetTracerHome = Join-Path $AppDirectory "datadog"
    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}

if ($IsLinux) {
    Write-Host "Using Linux configuration"
    $AppDirectory = Get-ApplicationDirectory

    $ClrProfilerPath = ""
    if ($Runtime -eq "x86") {
        Write-Error "x86 process architectures on Linux are not supported - only x64 process architectures are"
        Return 1
    }
    else {
        $ClrProfilerPath = Join-Path $AppDirectory "/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
        # if Alpine Linux x64
        # $ClrProfilerPath = Join-Path $AppDirectory "/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so"
        # if Linux ARM64
        # $ClrProfilerPath = Join-Path $AppDirectory "/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so"
    }

    # Depending on 32-bit vs 64-bit the path to the profiler will change
    Set-EnvironmentVariableForProcess -name "CORECLR_ENABLE_PROFILING" -value "1"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER" -value "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    Set-EnvironmentVariableForProcess -name "CORECLR_PROFILER_PATH" -value $ClrProfilerPath

    $DotNetTracerHome = Join-PAth $AppDirectory "datadog"
    Set-EnvironmentVariableForProcess -name "DD_DOTNET_TRACER_HOME" -value $DotNetTracerHome

    BuildAndRunSample
}
