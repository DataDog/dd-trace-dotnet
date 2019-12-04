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
    # TODO
    #  1. Get full path name of msi file

    $msifiles = Get-Item -Path d:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\*.msi

    $fullMsiPath = "d:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\" + $msifiles[0].Name

    Write-Host "About to run installer: " $fullMsiPath

    If (-Not (Test-Path $fullMsiPath ))
    {
        Write-Host "Could not find $fullMsiPath"
        exit 1
    }

    Write-Host "About to do msi installation..."

    Start-Process "msiexec.exe" -NoNewWindow -Wait -ArgumentList '/I d:\a\1\s\deploy\Datadog.Trace.ClrProfiler.WindowsInstaller\bin\Release\x64\en-us\datadog-dotnet-apm-1.9.1-prerelease-x64.msi /quiet'

    

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

