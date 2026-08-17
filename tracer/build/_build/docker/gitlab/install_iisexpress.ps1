param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Sha256,
    [Parameter(Mandatory=$true)][string]$Url
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$out = "$PSScriptRoot\iisexpress.msi"
$executable = "${env:ProgramFiles}\IIS Express\iisexpress.exe"

Write-Host -ForegroundColor Green "Downloading IIS Express $Version from $Url"
(New-Object System.Net.WebClient).DownloadFile($Url, $out)

$actualSha256 = (Get-FileHash -Algorithm SHA256 $out).Hash
if ($actualSha256 -ne $Sha256) {
    throw "Wrong checksum for ${out}: got '$actualSha256', expected '$Sha256'."
}

Write-Host -ForegroundColor Green "Installing IIS Express $Version"
$installer = Start-Process msiexec.exe -ArgumentList @('/i', $out, '/quiet', '/norestart') -Wait -PassThru
if ($installer.ExitCode -notin @(0, 3010)) {
    throw "IIS Express installer exited with code $($installer.ExitCode)."
}

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "IIS Express executable was not installed at '$executable'."
}

Remove-Item -LiteralPath $out
Write-Host -ForegroundColor Green "Done installing IIS Express $Version"
