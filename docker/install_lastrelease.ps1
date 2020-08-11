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
} 
else 
{
    Invoke-WebRequest -Uri $linux_url -OutFile linux.tar.gz
    mkdir release
    echo "Extracting linux.tar.gz"
    tar -xvzf linux.tar.gz -C ./release
    Remove-Item linux.tar.gz
}

