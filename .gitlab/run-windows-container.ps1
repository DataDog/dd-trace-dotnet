param (
    [string]$Image,
    # Zero leaves Docker's memory limit unspecified.
    [ValidateScript({ $_ -ge 0 })]
    [long]$MemoryLimitBytes = 20GB,
    [string]$ContainerName,
    [string]$EntryPoint,
    [string[]]$Environment = @(),
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]$Command
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Image)) {
    $Image = & "$PSScriptRoot/get-windows-build-image.ps1"
}

# Callers supply signing/test credentials explicitly; never inherit the full environment.
$dockerArguments = @(
    'run', '--rm',
    '-v', "$(Get-Location):c:\mnt",
    '-e', 'CI_JOB_ID',
    '-e', 'CI_PROJECT_NAME',
    '-e', 'CI_JOB_NAME_SLUG',
    '-e', 'WINDOWS_BUILDER=true',
    '-e', 'AWS_NETWORKING=true',
    '-e', 'NUGET_CERT_REVOCATION_MODE=offline'
)
if ($MemoryLimitBytes -gt 0) {
    $dockerArguments += @('-m', $MemoryLimitBytes.ToString([Globalization.CultureInfo]::InvariantCulture))
}
if (-not [string]::IsNullOrWhiteSpace($ContainerName)) {
    $dockerArguments += @('--name', $ContainerName)
}
if (-not [string]::IsNullOrWhiteSpace($EntryPoint)) {
    $dockerArguments += @('--entrypoint', $EntryPoint)
}
foreach ($variable in $Environment) {
    if ($variable -notmatch '^[A-Za-z_][A-Za-z0-9_]*(=|$)') {
        throw 'Invalid container environment entry: expected NAME or NAME=VALUE'
    }
    $dockerArguments += @('-e', $variable)
}
$dockerArguments += $Image
$dockerArguments += $Command
& docker @dockerArguments
exit $LASTEXITCODE
