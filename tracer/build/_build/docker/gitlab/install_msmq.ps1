$ErrorActionPreference = 'Stop'

Import-Module ServerManager

$feature = Get-WindowsFeature -Name MSMQ-Server
if ($feature.Installed) {
    Write-Host -ForegroundColor Green 'MSMQ Server is already installed'
    exit 0
}

Write-Host -ForegroundColor Green 'Installing the MSMQ Server Windows feature'
$result = Install-WindowsFeature -Name MSMQ-Server
if (-not $result.Success) {
    throw "Installing MSMQ Server failed with exit code '$($result.ExitCode)'."
}

if ($result.RestartNeeded -eq 'Yes') {
    throw 'Installing MSMQ Server requires a restart, which cannot be completed while building the container image.'
}

$feature = Get-WindowsFeature -Name MSMQ-Server
if (-not $feature.Installed) {
    throw 'MSMQ Server was not installed successfully.'
}

Write-Host -ForegroundColor Green 'Done installing MSMQ Server'
