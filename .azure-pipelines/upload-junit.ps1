$resultFolder = "./tracer/build_data/results";
$service= "dd-trace-dotnet";
$files = [System.IO.Directory]::GetFiles($resultFolder, "*.xml", [System.IO.SearchOption]::AllDirectories);

if ($IsLinux) { 
    Invoke-WebRequest -Uri "https://github.com/DataDog/datadog-ci/releases/latest/download/datadog-ci_linux-x64" -OutFile "/usr/local/bin/datadog-ci"
    chmod +x /usr/local/bin/datadog-ci
} elseif ($IsMacOS) { 
    Invoke-WebRequest -Uri "https://github.com/DataDog/datadog-ci/releases/latest/download/datadog-ci_darwin-x64" -OutFile "/usr/local/bin/datadog-ci"
    chmod +x /usr/local/bin/datadog-ci
} else {
    Invoke-WebRequest -Uri "https://github.com/DataDog/datadog-ci/releases/latest/download/datadog-ci_win-x64.exe" -OutFile "datadog-ci.exe"
}

$osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant();

foreach ($file in $files)
{
    $fileInfo = New-Object -TypeName System.IO.FileInfo -ArgumentList $file
    $file = $fileInfo.FullName;
    $targetFramework = $fileInfo.Directory.Name;

    $osPlatform = "windows";
    if ($IsLinux) { $osPlatform = "linux"; }
    if ($IsMacOS) { $osPlatform = "macos"; }

    $runtimeName = ".NET";
    $runtimeVersion = $targetFramework;
    switch ( $targetFramework )
    {
        NETCoreApp21 { $runtimeVersion = "2.1.0"; }
        NETCoreApp30 { $runtimeVersion = "3.0.0"; }
        NETCoreApp31 { $runtimeVersion = "3.1.0"; }
        NETCoreApp50 { $runtimeVersion = "5.0.0"; }
        NETCoreApp60 { $runtimeVersion = "6.0.0"; }
        NETFramework461 { $runtimeVersion = "4.6.1"; $runtimeName = ".NET Framework"; }
    }

    $env:DD_TAGS="os.platform:$osPlatform,os.architecture:$osArchitecture,runtime.name:$runtimeName,runtime.version:$runtimeVersion,runtime.vendor:$targetFramework,language:dotnet"

    Write-Output $file;
    Write-Output $env:DD_TAGS;

    if ($IsLinux) { 
        datadog-ci junit upload --service $service $file
    } elseif ($IsMacOS) { 
        datadog-ci junit upload --service $service $file
    } else {
        ./datadog-ci.exe junit upload --service $service $file
    }

    Write-Output "";
}