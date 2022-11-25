param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Sha512,
    [Parameter(Mandatory=$true)][string]$Url
)

# Enabled TLS12
$ErrorActionPreference = 'Stop'

# Script directory is $PSScriptRoot

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$out = "$($PSScriptRoot)\dotnetcoresdk.exe"

Write-Host -ForegroundColor Green Downloading $Url to $out
(New-Object System.Net.WebClient).DownloadFile($Url, $out)
if ((Get-FileHash -Algorithm SHA512 $out).Hash -ne "$Sha512") { Write-Host \"Wrong hashsum for ${out}: got '$((Get-FileHash -Algorithm SHA512 $out).Hash)', expected '$Sha512'.\"; exit 1 }

# Skip extraction of XML docs - generally not useful within an image/container - helps performance
setx NUGET_XMLDOC_MODE skip
Write-Host -ForegroundColor Green "Installing dotnetcore"

start-process -FilePath $out -ArgumentList "/install /quiet /norestart" -wait

Remove-Item $out

# Trigger first run experience by running arbitrary cmd
dotnet help

Write-Host -ForegroundColor Green Done with DotNet Core SDK $Version