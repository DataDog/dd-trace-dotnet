$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host -ForegroundColor Green "========= Installing python packages  ========="
& python -m pip install -r "$($PSScriptRoot)\requirements.txt"