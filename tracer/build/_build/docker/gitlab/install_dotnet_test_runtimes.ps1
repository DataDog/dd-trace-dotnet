$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Additional test runtimes: the SDK supplies .NET 11; the base image supplies .NET Framework 4.8.
$runtimes = Get-Content -Raw -LiteralPath "$PSScriptRoot/additional-dotnet-runtimes.json" | ConvertFrom-Json
$dotnetExecutable = Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'
$installedRuntimes = & $dotnetExecutable --list-runtimes
if ($LASTEXITCODE -ne 0) { throw 'Could not list installed .NET runtimes' }

foreach ($runtime in $runtimes) {
    $version = $runtime.version
    if ($version -notmatch '^\d+\.\d+\.\d+$' -or $runtime.sha512 -notmatch '^[a-fA-F0-9]{128}$') {
        throw 'Invalid pinned .NET runtime version or SHA512'
    }

    $runtimePattern = "^Microsoft\.NETCore\.App $([regex]::Escape($version)) "
    if ($installedRuntimes -match $runtimePattern) {
        Write-Host ".NET $version x64 runtime is already installed."
        continue
    }

    $installer = Join-Path $PSScriptRoot "dotnet-runtime-$version-win-x64.exe"
    try {
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Write-Host "Downloading .NET $version x64 runtime (attempt $attempt of 3)"
                Invoke-WebRequest -UseBasicParsing -Uri $runtime.url -OutFile $installer -TimeoutSec 120
                break
            }
            catch {
                if ($attempt -eq 3) { throw }
                Start-Sleep -Seconds (5 * $attempt)
            }
        }

        if ((Get-FileHash -LiteralPath $installer -Algorithm SHA512).Hash -ne $runtime.sha512) {
            throw "SHA512 mismatch for .NET $version runtime installer"
        }

        Write-Host "Installing .NET $version x64 runtime"
        $process = Start-Process -FilePath $installer -ArgumentList '/install /quiet /norestart' -WindowStyle Hidden -PassThru
        # Cache the handle so Windows PowerShell can retrieve ExitCode after waiting.
        $null = $process.Handle
        # This script runs only in Docker image preparation; a failure ends the RUN container.
        if (-not $process.WaitForExit(600000)) {
            throw ".NET $version runtime installer exceeded ten minutes"
        }
        if ($process.ExitCode -notin @(0, 3010)) {
            throw ".NET $version runtime installer exited with code $($process.ExitCode)"
        }

        $installedRuntimes = & $dotnetExecutable --list-runtimes
        if ($LASTEXITCODE -ne 0 -or -not ($installedRuntimes -match $runtimePattern)) {
            throw "The .NET $version x64 runtime was not installed"
        }
    }
    finally {
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }
}

$installedRuntimes | ForEach-Object { Write-Host $_ }
