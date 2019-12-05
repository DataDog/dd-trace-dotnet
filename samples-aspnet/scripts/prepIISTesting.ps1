# For now, just create the web apps for aspnet samples. Later, may want to use parameters to generalize.
#
# param (
#    [string]$name,
#    [string]$path
# )

$siteName = 'Default Web Site'

function createIISWebApps
{
    New-WebApplication -Site $siteName -Name Samples.AspNetMvc4 -PhysicalPath C:\src\DataDog\dd-trace-dotnet\samples-aspnet\Samples.AspNetMvc4 -Force -ErrorAction Stop
    New-WebApplication -Site $siteName -Name Samples.AspNetMvc5 -PhysicalPath C:\src\DataDog\dd-trace-dotnet\samples-aspnet\Samples.AspNetMvc5 -Force -ErrorAction Stop
}

function installDotnetTracer
{
    $msifiles = Get-Item -Path d:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\*.msi
    $basefilename = $msifiles[0].Name

    Write-Host 'number msi files: ' $msifiles.Count
    Write-Host 'name of file: ' $msifiles[0].Name

    # $fullMsiPath = "D:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\" + $msifiles[0].Name
    $fullMsiPath = 'D:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\'
    $fullMsiPath += $basefilename

    # $fullMsiPath = "D:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\datadog-dotnet-apm-1.10.0-x64.msi"

    Write-Host "About to run installer: " $fullMsiPath

    If (-Not (Test-Path $fullMsiPath )) 
    {
        Write-Host "Could not find $fullMsiPath"
        exit 1
    }

    $arguments = '/I '
    $arguments += $fullMsiPath
    $arguments += ' /quiet'
    Start-Process "msiexec.exe" -NoNewWindow -Wait -ArgumentList $arguments
    # Start-Process "msiexec.exe" -NoNewWindow -Wait -ArgumentList '/I $fullMsiPath /quiet'
    # Start-Process "msiexec.exe" -NoNewWindow -Wait -ArgumentList '/I D:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\datadog-dotnet-apm-1.10.0-x64.msi /quiet'

    if( -not $? )
    {
        $msg = $Error[0].Exception.Message
        Write-Host "Encountered error during MSI installation. Error Message is $msg. Please check."
    }
}

function VerifyInstall
{
    $instpath = $env:ProgramFiles
    $instpath += '\Datadog'

    If (-Not (Test-Path $instpath ))
    {
        Write-Host 'No installation path found: ' $instpath
    }
    Else
    {
        Write-Host 'Found installation path: ' $instpath
    }

}


# Main entry point of script

createIISWebApps
installDotnetTracer
VerifyInstall

