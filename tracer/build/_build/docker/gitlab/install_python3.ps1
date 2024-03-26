param (
    [Parameter(Mandatory=$true)][string]$Version
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ApplicationName = "Python"
$Installer = "$($PSScriptRoot)\python.exe"
$DownloadUrl = "https://www.python.org/ftp/python/$Version/python-$Version-amd64.exe"
$InstallDir = "c:\tmp\Python38"
$PythonScriptsDir = "c:\tmp\Python38\scripts"

Write-Host -ForegroundColor Green "========= Installing Python $Version ========="

# Download installer
Write-Host "Downloading $ApplicationName"
(New-Object System.Net.WebClient).DownloadFile($DownloadUrl, $Installer)

# Start the installer
Write-Host "Installing $ApplicationName"
Start-Process $Installer -ArgumentList '/quiet InstallAllUsers=1 TargetDir=C:\tmp\Python38' -Wait

# Test to make sure the application actually installed.
if (!(Test-Path $InstallDir)) {
    throw "FATAL: '$ApplicationName' was not found after MSI installation"
}
else {
    # Cleanup
    Remove-Item $Installer
    # Add application to PATH
    [Environment]::SetEnvironmentVariable("Path", $env:Path + ";$InstallDir;$PythonScriptsDir", [EnvironmentVariableTarget]::Machine)
    Write-Host -ForegroundColor Green "$ApplicationName has been installed successfully"
}