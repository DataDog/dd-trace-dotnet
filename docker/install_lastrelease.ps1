echo "Downloading tracer binaries..."
$release_version = $env:releaseVersion

if ([string]::IsNullOrWhiteSpace($release_version)) {
    $release_version = "1.19.1"
}

$windows_url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$($release_version)/windows-tracer-home.zip"
$linux_url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$($release_version)/datadog-dotnet-apm-$($release_version).tar.gz"

$dd_tracer_home = ""
$dd_tracer_integrations = ""
$dd_tracer_profiler_32 = ""
$dd_tracer_profiler_64 = ""

if ($env:os -eq "Windows_NT") 
{
    Invoke-WebRequest -Uri $windows_url -OutFile windows.zip
    echo "Extracting windows.zip"
    Expand-Archive windows.zip -DestinationPath .\release
    Remove-Item windows.zip

    $dd_tracer_home = "$(pwd)\release"
    $dd_tracer_integrations = "$dd_tracer_home\integrations.json"
    $dd_tracer_profiler_32 = "$dd_tracer_home\win-x86\Datadog.Trace.ClrProfiler.Native.dll"
    $dd_tracer_profiler_64 = "$dd_tracer_home\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
} 
else 
{
    Invoke-WebRequest -Uri $linux_url -OutFile linux.tar.gz
    mkdir release
    echo "Extracting linux.tar.gz"
    tar -xvzf linux.tar.gz -C ./release
    Remove-Item linux.tar.gz
    
    $dd_tracer_home = "$(pwd)/release"
    $dd_tracer_integrations = "$dd_tracer_home/integrations.json"
    $dd_tracer_profiler_64 = "$dd_tracer_home/Datadog.Trace.ClrProfiler.Native.so"
}

echo "##vso[task.setvariable variable=DD_DOTNET_TRACER_HOME]$dd_tracer_home"
echo "##vso[task.setvariable variable=DD_INTEGRATIONS]$dd_tracer_integrations"

echo "##vso[task.setvariable variable=CORECLR_ENABLE_PROFILING]1"
echo "##vso[task.setvariable variable=CORECLR_PROFILER]{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
echo "##vso[task.setvariable variable=CORECLR_PROFILER_PATH_32]$dd_tracer_profiler_32"
echo "##vso[task.setvariable variable=CORECLR_PROFILER_PATH_64]$dd_tracer_profiler_64"

echo "##vso[task.setvariable variable=COR_ENABLE_PROFILING]1"
echo "##vso[task.setvariable variable=COR_PROFILER]{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
echo "##vso[task.setvariable variable=COR_PROFILER_PATH_32]$dd_tracer_profiler_32"
echo "##vso[task.setvariable variable=COR_PROFILER_PATH_64]$dd_tracer_profiler_64"
