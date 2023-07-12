$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$TargetContainer = $true
. "$($PSScriptRoot)\helpers.ps1"
Write-Host -ForegroundColor Green "Installing Windows Codesign Helper $ENV:WINSIGN_VERSION"

## need to have more rigorous download at some point, but
$codesign_base = "windows_code_signer-$($ENV:WINSIGN_VERSION)-py3-none-any.whl"
$codesign_wheel = "https://s3.amazonaws.com/dd-agent-omnibus/windows-code-signer/$($codesign_base)"
$codesign_wheel_target = "c:\devtools\$($codesign_base)"
(New-Object System.Net.WebClient).DownloadFile($codesign_wheel, $codesign_wheel_target)

Get-RemoteFile -RemoteFile $codesign_wheel -LocalFile $codesign_wheel_target -VerifyHash $ENV:WINSIGN_SHA256

python -m pip install $codesign_wheel_target