$outPath = $env:TEMP # change this to save the msi and log file to a different path

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host 'Finding latest release of Datadog SDK for .NET.'
$releases = 'https://api.github.com/repos/DataDog/dd-trace-dotnet/releases/latest'
$tag = (Invoke-WebRequest $releases -UseBasicParsing | ConvertFrom-Json)[0].tag_name
$version = $tag.Substring(1)
$msiFilename = "$outPath\datadog-dotnet-apm-$version-x64.msi"
$logFilename = "$outPath\datadog-dotnet-apm-$version-x64.log"
$url = "https://github.com/DataDog/dd-trace-dotnet/releases/download/$tag/datadog-dotnet-apm-$version-x64.msi"

Write-Host "Found installer for Datadog SDK for .NET $tag"
Write-Host "- downloading from ""$url"""
Write-Host "- saving to ""$msiFilename"""
Invoke-WebRequest -Uri $url -OutFile $msiFilename

Write-Host "Installing. Logs will be saved to ""$logFilename""."
Start-Process -Wait 'C:\Windows\system32\msiexec.exe' -ArgumentList "/i ""$msiFilename"" /quiet /qn /norestart /log ""$logFilename"""

Write-Host "Installation finished. Deleting installer file ""$msiFilename""."
Remove-Item $msiFilename
