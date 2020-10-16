$ProgressPreference = 'SilentlyContinue'

echo "Getting latest release version"
# Get the latest release tag from the github release page
$release_version = (Invoke-WebRequest https://api.github.com/repos/datadog/dd-trace-dotnet/releases | ConvertFrom-Json)[0].tag_name.SubString(1)

$dd_tracer_workingfolder = $env:DD_TRACER_WORKINGFOLDER
$dd_tracer_home = ""
$dd_tracer_msbuild = ""
$dd_tracer_integrations = ""
$dd_tracer_profiler_32 = ""
$dd_tracer_profiler_64 = ""


# Download the binary file depending of the current operating system and extract the content to the "release" folder 
echo "Downloading tracer v$release_version"
if ($env:os -eq "Windows_NT") 
{
    $url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$($release_version)/windows-tracer-home.zip"

    Invoke-WebRequest -Uri $url -OutFile windows.zip
    echo "Extracting windows.zip"
    Expand-Archive windows.zip -DestinationPath .\release
    Remove-Item windows.zip

    if ([string]::IsNullOrEmpty($dd_tracer_workingfolder)) {
        $dd_tracer_home = "$(pwd)\release"
    } else {
        $dd_tracer_home = "$dd_tracer_workingfolder\release"
    }

    $dd_tracer_msbuild = "$dd_tracer_home\netstandard2.0\Datadog.Trace.MSBuild.dll"
    $dd_tracer_integrations = "$dd_tracer_home\integrations.json"
    $dd_tracer_profiler_32 = "$dd_tracer_home\win-x86\Datadog.Trace.ClrProfiler.Native.dll"
    $dd_tracer_profiler_64 = "$dd_tracer_home\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
} 
else 
{
    # File version is the same as the release version without the prerelease suffix.
    $file_version = $release_version.replace("-prerelease", "")

    $url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$($release_version)/datadog-dotnet-apm-$($file_version).tar.gz"

    Invoke-WebRequest -Uri $url -OutFile linux.tar.gz
    mkdir release
    echo "Extracting linux.tar.gz"
    tar -xvzf linux.tar.gz -C ./release
    Remove-Item linux.tar.gz
    
    if ([string]::IsNullOrEmpty($dd_tracer_workingfolder)) {
        $dd_tracer_home = "$(pwd)/release"
    } else {
        $dd_tracer_home = "$dd_tracer_workingfolder/release"
    }

    $dd_tracer_msbuild = "$dd_tracer_home/netstandard2.0/Datadog.Trace.MSBuild.dll"
    $dd_tracer_integrations = "$dd_tracer_home/integrations.json"
    $dd_tracer_profiler_64 = "$dd_tracer_home/Datadog.Trace.ClrProfiler.Native.so"
}

# Set all environment variables to attach the profiler to the following pipeline steps
echo "Setting environment variables..."

echo "##vso[task.setvariable variable=DD_ENV]CI"
echo "##vso[task.setvariable variable=DD_DOTNET_TRACER_HOME]$dd_tracer_home"
echo "##vso[task.setvariable variable=DD_DOTNET_TRACER_MSBUILD]$dd_tracer_msbuild"
echo "##vso[task.setvariable variable=DD_INTEGRATIONS]$dd_tracer_integrations"

echo "##vso[task.setvariable variable=CORECLR_ENABLE_PROFILING]1"
echo "##vso[task.setvariable variable=CORECLR_PROFILER]{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
echo "##vso[task.setvariable variable=CORECLR_PROFILER_PATH_32]$dd_tracer_profiler_32"
echo "##vso[task.setvariable variable=CORECLR_PROFILER_PATH_64]$dd_tracer_profiler_64"

echo "##vso[task.setvariable variable=COR_ENABLE_PROFILING]1"
echo "##vso[task.setvariable variable=COR_PROFILER]{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
echo "##vso[task.setvariable variable=COR_PROFILER_PATH_32]$dd_tracer_profiler_32"
echo "##vso[task.setvariable variable=COR_PROFILER_PATH_64]$dd_tracer_profiler_64"

echo "Done."