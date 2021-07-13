Param (
    [Parameter(Mandatory)]
    $ProjectDirectory,

    [Parameter(Mandatory)]
    [String]
    $HomeVersion,

    [Parameter(Mandatory)]
    [String]
    $NuGetVersion,

    [Parameter(Mandatory)]
    [String]
    $TracerHomesDirectory,

    [String]
    $Framework = "net5.0",

    [String]
    $LogPath = (Join-Path $TracerHomesDirectory "logs"),

    [Int32]
    $ServerPort = 15000,

    [Bool]
    $RemoveLogsBeforeRun = $true
)

$ErrorActionPreference = 'Stop'

$ProjectDirectory = Resolve-Path $ProjectDirectory
$TracerHomeDirectory = Resolve-Path (Join-Path $TracerHomesDirectory "tracer-home-${HomeVersion}")
$LogPath = Resolve-Path $LogPath

if ($RemoveLogsBeforeRun) {
    Write-Output "Killing dotnet.exe and deleting log files from ${LogPath}."
    Stop-Process -Name 'dotnet.exe' -ErrorAction 'SilentlyContinue'
    Start-Sleep -Seconds 2
    Remove-Item "${LogPath}\*"
}

Push-Location $ProjectDirectory

try {
    # set env var used by "nuget.config" file to find nuget packages
    $env:TracerHomesDirectory = Resolve-Path $TracerHomesDirectory

    Write-Output "Building sample app with NuGet package ${NuGetVersion}."
    dotnet clean -v m
    dotnet restore "-p:DatadogTraceNuGetVersion=${NuGetVersion}" --force --no-cache -v m
    dotnet build --no-restore -v m

    Write-Output "Setting tracer home to ${TracerHomeDirectory} and log path to ${LogPath}."
    & $PSScriptRoot\enable-tracer.ps1 -TracerHome $TracerHomeDirectory -LogPath $LogPath

    Write-Output "Starting sample application..."
    dotnet run -f $Framework --no-build --urls "http://localhost:${ServerPort}"
}
finally {
    Pop-Location
    Remove-Item env:TracerHomesDirectory, env:COR_ENABLE_PROFILING, env:CORECLR_ENABLE_PROFILING -ErrorAction "SilentlyContinue"
}
