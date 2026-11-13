param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Url
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$installerPath = "$PSScriptRoot\azure-functions-core-tools.msi"
$installLogPath = "$PSScriptRoot\azure-functions-core-tools-install.log"
$executable = "${env:ProgramFiles}\Microsoft\Azure Functions Core Tools\func.exe"

Write-Host -ForegroundColor Green "Downloading Azure Functions Core Tools $Version from $Url"
(New-Object System.Net.WebClient).DownloadFile($Url, $installerPath)

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
$signer = $signature.SignerCertificate.Subject
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid `
    -or $signer -notlike '*Microsoft Corporation*') {
    throw "Azure Functions Core Tools installer does not have a valid Microsoft signature: status=$($signature.Status), signer='$signer'."
}

Write-Host -ForegroundColor Green "Installing Azure Functions Core Tools $Version"
$installer = Start-Process msiexec.exe -ArgumentList @('/i', $installerPath, '/quiet', '/norestart', '/l*v', $installLogPath) -Wait -PassThru
if ($installer.ExitCode -notin @(0, 3010)) {
    if (Test-Path -LiteralPath $installLogPath -PathType Leaf) {
        Write-Host 'Last 200 lines from the Azure Functions Core Tools installer log:'
        Get-Content -LiteralPath $installLogPath -Tail 200
    }

    throw "Azure Functions Core Tools installer exited with code $($installer.ExitCode)."
}

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Azure Functions Core Tools executable was not installed at '$executable'."
}

$versionOutput = @(& $executable --version)
$versionExitCode = $LASTEXITCODE
$installedVersion = $versionOutput | Where-Object { $_ -match '^\s*\d+\.\d+\.\d+' } | Select-Object -First 1
if ($versionExitCode -ne 0 -or $null -eq $installedVersion -or $installedVersion.Trim() -notlike "$Version*") {
    throw "Expected Azure Functions Core Tools $Version but found '$installedVersion'."
}

$installedVersion = $installedVersion.Trim()

Remove-Item -LiteralPath $installerPath
Remove-Item -LiteralPath $installLogPath -ErrorAction SilentlyContinue
Write-Host -ForegroundColor Green "Done installing Azure Functions Core Tools $installedVersion"
