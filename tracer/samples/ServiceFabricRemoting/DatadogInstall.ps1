# allow overriding defaults using environment variables
if (Test-Path env:SvcFabDir) { $SvcFabDir = $env:SvcFabDir } else { $SvcFabDir = 'D:\SvcFab' }
if (Test-Path env:DD_TRACER_VERSION) { $DD_TRACER_VERSION = $env:DD_TRACER_VERSION } else { $DD_TRACER_VERSION = '1.24.0' }
if (Test-Path env:DD_TRACER_URL) { $DD_TRACER_URL = $env:DD_TRACER_URL } else { $DD_TRACER_URL = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$DD_TRACER_VERSION/windows-tracer-home.zip" }
if (Test-Path env:DD_DOTNET_TRACER_HOME) { $DD_DOTNET_TRACER_HOME = $env:DD_DOTNET_TRACER_HOME } else { $DD_DOTNET_TRACER_HOME = "$SvcFabDir\datadog-dotnet-tracer\v$DD_TRACER_VERSION" }

Write-Host "[DatadogInstall.ps1] Installing Datadog .NET Tracer v$DD_TRACER_VERSION"

# download, extract, and delete the archive
$ArchivePath = "$SvcFabDir\windows-tracer-home.zip"
Write-Host "[DatadogInstall.ps1] Downloading $DD_TRACER_URL to $ArchivePath"
Invoke-WebRequest $DD_TRACER_URL -OutFile $ArchivePath

Write-Host "[DatadogInstall.ps1] Extracting to $DD_DOTNET_TRACER_HOME"
Expand-Archive -Force -Path "$SvcFabDir\windows-tracer-home.zip" -DestinationPath $DD_DOTNET_TRACER_HOME

Write-Host "[DatadogInstall.ps1] Deleting $ArchivePath"
Remove-Item $ArchivePath

# create a folder for log files
$LOGS_PATH = "$SvcFabDir\datadog-dotnet-tracer-logs"

if (-not (Test-Path -Path $LOGS_PATH -PathType Container)) {
  Write-Host "[DatadogInstall.ps1] Creating logs folder $LOGS_PATH"
  New-Item -ItemType Directory -Force -Path $LOGS_PATH
}

function Set-MachineEnvironmentVariable {
    param(
      [string]$name,
      [string]$value
    )

    Write-Host "[DatadogInstall.ps1] Setting environment variable $name=$value"
    [System.Environment]::SetEnvironmentVariable($name, $value, [System.EnvironmentVariableTarget]::Machine)
}

Set-MachineEnvironmentVariable 'DD_DOTNET_TRACER_HOME' $DD_DOTNET_TRACER_HOME
Set-MachineEnvironmentVariable 'DD_INTEGRATIONS' "$DD_DOTNET_TRACER_HOME\integrations.json"
Set-MachineEnvironmentVariable 'DD_TRACE_LOG_DIRECTORY' "$LOGS_PATH"

# Set-MachineEnvironmentVariable'COR_ENABLE_PROFILING' '0' # Enable per app
Set-MachineEnvironmentVariable 'COR_PROFILER' '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
Set-MachineEnvironmentVariable 'COR_PROFILER_PATH_32' "$DD_DOTNET_TRACER_HOME\win-x86\Datadog.Trace.ClrProfiler.Native.dll"
Set-MachineEnvironmentVariable 'COR_PROFILER_PATH_64' "$DD_DOTNET_TRACER_HOME\win-x64\Datadog.Trace.ClrProfiler.Native.dll"

# Set-MachineEnvironmentVariable 'CORECLR_ENABLE_PROFILING' '0' # Enable per app
Set-MachineEnvironmentVariable 'CORECLR_PROFILER' '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
Set-MachineEnvironmentVariable 'CORECLR_PROFILER_PATH_32' "$DD_DOTNET_TRACER_HOME\win-x86\Datadog.Trace.ClrProfiler.Native.dll"
Set-MachineEnvironmentVariable 'CORECLR_PROFILER_PATH_64' "$DD_DOTNET_TRACER_HOME\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
