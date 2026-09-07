param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Sha256,
    [Parameter(Mandatory=$true)][string]$Url
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$bootstrapper = "$PSScriptRoot\SQL2022-SSEI-Expr.exe"
$mediaPath = "$PSScriptRoot\localdb-media"
$executable = "${env:ProgramFiles}\Microsoft SQL Server\160\Tools\Binn\SqlLocalDB.exe"

Write-Host -ForegroundColor Green "Downloading SQL Server Express LocalDB $Version bootstrapper from $Url"
(New-Object System.Net.WebClient).DownloadFile($Url, $bootstrapper)

$actualSha256 = (Get-FileHash -Algorithm SHA256 $bootstrapper).Hash
if ($actualSha256 -ne $Sha256) {
    throw "Wrong checksum for ${bootstrapper}: got '$actualSha256', expected '$Sha256'."
}

Write-Host -ForegroundColor Green "Downloading SQL Server Express LocalDB $Version installation media"
$download = Start-Process $bootstrapper -ArgumentList @('/Action=Download', '/MediaType=LocalDB', "/MediaPath=$mediaPath", '/Quiet') -Wait -PassThru
if ($download.ExitCode -ne 0) {
    throw "SQL Server Express LocalDB media download exited with code $($download.ExitCode)."
}

$installerPath = Get-ChildItem -LiteralPath $mediaPath -Filter SqlLocalDB.msi -Recurse -File |
                     Select-Object -First 1 -ExpandProperty FullName
if (-not $installerPath) {
    throw "SQL Server Express LocalDB installer was not found under '$mediaPath'."
}

Write-Host -ForegroundColor Green "Installing SQL Server Express LocalDB $Version"
$logPath = "$PSScriptRoot\localdb-install.log"
$installerArguments = @(
    '/i',
    $installerPath,
    '/quiet',
    '/norestart',
    'IACCEPTSQLLOCALDBLICENSETERMS=YES',
    'SKIPPENDINGREBOOTCHECK=1',
    'REBOOT=ReallySuppress',
    '/l*v',
    $logPath
)
$installer = Start-Process msiexec.exe -ArgumentList $installerArguments -Wait -PassThru
if ($installer.ExitCode -notin @(0, 3010)) {
    if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Write-Host 'Last 200 lines from the SQL Server Express LocalDB installer log:'
        Get-Content -LiteralPath $logPath -Tail 200
    }

    throw "SQL Server Express LocalDB installer exited with code $($installer.ExitCode)."
}

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "SQL Server Express LocalDB executable was not installed at '$executable'."
}

$machinePath = [Environment]::GetEnvironmentVariable('Path', [EnvironmentVariableTarget]::Machine)
$localDbToolsPath = Split-Path -Parent $executable
[Environment]::SetEnvironmentVariable('Path', "$machinePath;$localDbToolsPath", [EnvironmentVariableTarget]::Machine)

Remove-Item -LiteralPath $bootstrapper
Remove-Item -LiteralPath $mediaPath -Recurse
Remove-Item -LiteralPath $logPath
Write-Host -ForegroundColor Green "Done installing SQL Server Express LocalDB $Version"
