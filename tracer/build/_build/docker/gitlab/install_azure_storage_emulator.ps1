param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Url
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$installerPath = "$PSScriptRoot\azure-storage-emulator.msi"
$installLogPath = "$PSScriptRoot\azure-storage-emulator-install.log"
$executable = "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe"

Write-Host -ForegroundColor Green "Downloading Azure Storage Emulator $Version from $Url"
(New-Object System.Net.WebClient).DownloadFile($Url, $installerPath)

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
$signer = $signature.SignerCertificate.Subject
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid `
    -or $signer -notlike '*Microsoft Corporation*') {
    throw "Azure Storage Emulator installer does not have a valid Microsoft signature: status=$($signature.Status), signer='$signer'."
}

Write-Host -ForegroundColor Green "Installing Azure Storage Emulator $Version"
$installer = Start-Process msiexec.exe -ArgumentList @('/i', $installerPath, '/quiet', '/norestart', '/l*v', $installLogPath) -Wait -PassThru
if ($installer.ExitCode -notin @(0, 3010)) {
    if (Test-Path -LiteralPath $installLogPath -PathType Leaf) {
        Write-Host 'Last 200 lines from the Azure Storage Emulator installer log:'
        Get-Content -LiteralPath $installLogPath -Tail 200
    }

    throw "Azure Storage Emulator installer exited with code $($installer.ExitCode)."
}

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Azure Storage Emulator executable was not installed at '$executable'."
}

$installedVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
if ($installedVersion -notlike "$Version*") {
    throw "Expected Azure Storage Emulator $Version but found '$installedVersion'."
}

Remove-Item -LiteralPath $installerPath
Remove-Item -LiteralPath $installLogPath -ErrorAction SilentlyContinue
Write-Host -ForegroundColor Green "Done installing Azure Storage Emulator $installedVersion"
