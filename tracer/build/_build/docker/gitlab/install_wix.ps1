param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Sha256
)

# Enabled TLS12
$ErrorActionPreference = 'Stop'

# Script directory is $PSScriptRoot

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$shortenedver = $Version.Replace('.','')
$splitver = $Version.split(".")
$majmin = "$($splitver[0])$($splitver[1])"

# https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311.exe
$wixzip = "https://github.com/wixtoolset/wix3/releases/download/wix$($shortenedver)rtm/wix$($majmin).exe"

Write-Host  -ForegroundColor Green starting with WiX
$out = "$($PSScriptRoot)\wix.exe"
(New-Object System.Net.WebClient).DownloadFile($wixzip, $out)
if ((Get-FileHash -Algorithm SHA256 $out).Hash -ne "$Sha256") { Write-Host \"Wrong hashsum for ${out}: got '$((Get-FileHash -Algorithm SHA256 $out).Hash)', expected '$Sha256'.\"; exit 1 }

Write-Host -ForegroundColor Green Done downloading wix, installing
Start-Process $out -ArgumentList '/q' -Wait

[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine) + ";${env:ProgramFiles(x86)}\WiX Toolset v${majmin}\bin\", [System.EnvironmentVariableTarget]::Machine)
[Environment]::SetEnvironmentVariable("WIX", "${env:ProgramFiles(x86)}\WiX Toolset ${majmin}\", [System.EnvironmentVariableTarget]::Machine)
Remove-Item $out

Write-Host -ForegroundColor Green Done with WiX