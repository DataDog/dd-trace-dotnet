echo "Downloading tracer binaries..."
$release_version = $env:releaseVersion

if ([string]::IsNullOrWhiteSpace($release_version)) {
    $release_version = "1.19.1";
}

$windows_url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$($release_version)/windows-tracer-home.zip"
$linux_url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$($release_version)/datadog-dotnet-apm-$($release_version).tar.gz"

if ($isWindows) 
{
    Invoke-WebRequest -Uri $windows_url -OutFile windows.zip
    echo "Extracting windows.zip"
    Expand-Archive windows.zip -DestinationPath .\release
    Remove-Item windows.zip

    [Environment]::SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", "$(pwd)\release", "User")
    [Environment]::SetEnvironmentVariable("DD_INTEGRATIONS", "$($env:DD_DOTNET_TRACER_HOME)\integrations.json", "User")

    [Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1", "User")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}", "User")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$($env:DD_DOTNET_TRACER_HOME)\win-x64\Datadog.Trace.ClrProfiler.Native.dll", "User")

    [Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING", "1", "User")
    [Environment]::SetEnvironmentVariable("COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}", "User")
    [Environment]::SetEnvironmentVariable("COR_PROFILER_PATH_32", "$($env:DD_DOTNET_TRACER_HOME)\win-x86\Datadog.Trace.ClrProfiler.Native.dll", "User")
    [Environment]::SetEnvironmentVariable("COR_PROFILER_PATH_64", "$($env:DD_DOTNET_TRACER_HOME)\win-x64\Datadog.Trace.ClrProfiler.Native.dll", "User")
} 
else 
{
    Invoke-WebRequest -Uri $linux_url -OutFile linux.tar.gz
    mkdir release
    echo "Extracting linux.tar.gz"
    tar -xvzf linux.tar.gz -C ./release
    Remove-Item linux.tar.gz

    [Environment]::SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", "$(pwd)/release", "User")
    [Environment]::SetEnvironmentVariable("DD_INTEGRATIONS", "$($env:DD_DOTNET_TRACER_HOME)/integrations.json", "User")

    [Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1", "User")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}", "User")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$($env:DD_DOTNET_TRACER_HOME)/Datadog.Trace.ClrProfiler.Native.so", "User")
}