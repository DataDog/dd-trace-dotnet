$ErrorActionPreference = 'Stop'

$func = Get-Command 'func.exe' -ErrorAction Stop
$storageEmulator = Get-Command 'AzureStorageEmulator.exe' -ErrorAction Stop

$versionOutput = @(& $func.Source --version)
$versionExitCode = $LASTEXITCODE
$funcVersion = $versionOutput | Where-Object { $_ -match '^\s*\d+\.\d+\.\d+' } | Select-Object -First 1
if ($versionExitCode -ne 0 -or $null -eq $funcVersion) {
    throw "func.exe exited with code $versionExitCode or did not report a recognizable version."
}

Write-Output "Azure Functions Core Tools: $($funcVersion.Trim())"

$started = $false
for ($attempt = 1; $attempt -le 5; $attempt++) {
    Write-Output "Starting Azure Storage Emulator (attempt $attempt of 5)"
    & $storageEmulator.Source start
    if ($LASTEXITCODE -eq 0) {
        $started = $true
        break
    }

    if ($attempt -lt 5) {
        Start-Sleep -Seconds 5
    }
}

if (-not $started) {
    throw 'Azure Storage Emulator could not be started after 5 attempts.'
}

& $storageEmulator.Source status
if ($LASTEXITCODE -ne 0) {
    throw "AzureStorageEmulator.exe exited with code $LASTEXITCODE while reporting its status."
}

Write-Output 'Azure Storage Emulator initialized'
