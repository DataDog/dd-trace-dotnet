$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

foreach ($package in $packages) {
    Write-Host -ForegroundColor Green "========= Installing $package  ========="
    & python -m pip install -r "$($PSScriptRoot)\requirements.txt"
}