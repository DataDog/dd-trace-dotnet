param (
    [Parameter(Mandatory=$true)]
    [string] $TracerHomePath,
    [string] $TracerVersion = '1.24.0',
    [string] $DownloadUrl = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$TracerVersion/windows-tracer-home.zip"
)

Write-Host "[DatadogInstall.ps1] Installing Datadog .NET Tracer v$TracerVersion"

if (-not (Test-Path -Path $TracerHomePath -PathType Container)) {
  Write-Host "[DatadogInstall.ps1] Creating folder $TracerHomePath"
  New-Item -ItemType Directory -Force -Path $TracerHomePath
}

# download, extract, and delete the archive
$ArchivePath = "$TracerHomePath\windows-tracer-home.zip"
Write-Host "[DatadogInstall.ps1] Downloading $DownloadUrl to $ArchivePath"
Invoke-WebRequest $DownloadUrl -OutFile $ArchivePath

Write-Host "[DatadogInstall.ps1] Extracting to $TracerHomePath"
Expand-Archive -Force -Path "$TracerHomePath\windows-tracer-home.zip" -DestinationPath $TracerHomePath

# create a folder for log files
$LogsPath = "$TracerHomePath\logs"

if (-not (Test-Path -Path $LogsPath -PathType Container)) {
  Write-Host "[DatadogInstall.ps1] Creating logs folder $LogsPath"
  New-Item -ItemType Directory -Force -Path $LogsPath
}