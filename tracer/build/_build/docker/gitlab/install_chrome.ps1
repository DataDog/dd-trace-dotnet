param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$ChromeUrl,
    [Parameter(Mandatory=$true)][string]$ChromeSha256,
    [Parameter(Mandatory=$true)][string]$ChromeDriverUrl,
    [Parameter(Mandatory=$true)][string]$ChromeDriverSha256
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$chromeArchive = "$PSScriptRoot\chrome-headless-shell.zip"
$driverArchive = "$PSScriptRoot\chromedriver.zip"
$chromeRoot = 'C:\devtools\chrome'
$driverRoot = 'C:\devtools\chromedriver'

Write-Host -ForegroundColor Green "Downloading Chrome for Testing $Version"
(New-Object System.Net.WebClient).DownloadFile($ChromeUrl, $chromeArchive)

Write-Host -ForegroundColor Green "Downloading ChromeDriver $Version"
(New-Object System.Net.WebClient).DownloadFile($ChromeDriverUrl, $driverArchive)

$actualChromeSha256 = (Get-FileHash -Algorithm SHA256 $chromeArchive).Hash
if ($actualChromeSha256 -ne $ChromeSha256) {
    throw "Wrong checksum for ${chromeArchive}: got '$actualChromeSha256', expected '$ChromeSha256'."
}

$actualChromeDriverSha256 = (Get-FileHash -Algorithm SHA256 $driverArchive).Hash
if ($actualChromeDriverSha256 -ne $ChromeDriverSha256) {
    throw "Wrong checksum for ${driverArchive}: got '$actualChromeDriverSha256', expected '$ChromeDriverSha256'."
}

New-Item -ItemType Directory -Path $chromeRoot, $driverRoot -Force | Out-Null
Expand-Archive -LiteralPath $chromeArchive -DestinationPath $chromeRoot -Force
Expand-Archive -LiteralPath $driverArchive -DestinationPath $driverRoot -Force

$chrome = Join-Path $chromeRoot 'chrome-headless-shell-win64\chrome-headless-shell.exe'
$driver = Join-Path $driverRoot 'chromedriver-win64\chromedriver.exe'
if (-not (Test-Path -LiteralPath $chrome -PathType Leaf)) {
    throw "Chrome for Testing executable was not installed at '$chrome'."
}

if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) {
    throw "ChromeDriver executable was not installed at '$driver'."
}

Remove-Item -LiteralPath $chromeArchive, $driverArchive
Write-Host -ForegroundColor Green "Done installing Chrome for Testing and ChromeDriver $Version"
