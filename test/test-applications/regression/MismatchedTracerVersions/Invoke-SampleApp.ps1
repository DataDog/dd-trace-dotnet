#Requires -Version 7

Param (
    [Parameter(Mandatory)]
    [ValidateScript( { Test-Path $_ -PathType 'Container' })]
    [String]
    $ProjectDirectory,

    [Parameter(Mandatory)]
    [String]
    $HomeVersion,

    [Parameter(Mandatory)]
    [String]
    $NuGetVersion,

    [ValidateScript( { Test-Path $_ -PathType 'Container' })]
    [String]
    $TracerHomesDirectory = (Join-Path $PSScriptRoot 'tracer-homes' -Resolve),

    [String]
    $Framework = "net5.0",

    [String]
    $LogPath = (Join-Path $TracerHomesDirectory 'logs'),

    [Int32]
    $ServerPort = 15000
)

$ErrorActionPreference = 'Stop'

$TracerHomeDirectory = Join-Path $TracerHomesDirectory ${HomeVersion} -Resolve
Push-Location $ProjectDirectory

try {
    # set env var used by "nuget.config" file to find nuget packages
    $env:TracerHomesDirectory = Resolve-Path $TracerHomesDirectory

    Write-Host "Building sample app with NuGet package ${NuGetVersion}."
    dotnet clean -v m || Write-Error "Native command returned code $LASTEXITCODE"
    dotnet restore "-p:DatadogTraceNuGetVersion=${NuGetVersion}" --force --no-cache -v m || Write-Error "Native command returned code $LASTEXITCODE"
    dotnet build -f $Framework --no-restore -v m || Write-Error "Native command returned code $LASTEXITCODE"

    Write-Host "Setting tracer home to  ${TracerHomeDirectory}."
    Write-Host "and tracer logs path to ${LogPath}."
    & "$PSScriptRoot\Enable-Tracer.ps1" -TracerHome $TracerHomeDirectory -LogPath $LogPath

    Write-Host 'Starting sample application...'
    dotnet run -f $Framework --no-build --urls "http://localhost:${ServerPort}"
    Write-Host "Sample app exited with code $LASTEXITCODE"
}
finally {
    Pop-Location
    Remove-Item 'env:TracerHomesDirectory' -ErrorAction 'Continue'
    & "$PSScriptRoot\Disable-Tracer.ps1"
}
