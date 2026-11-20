param (
    [Parameter(Mandatory=$true)]
    [string]$Framework
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$installDir = 'C:\Program Files\dotnet'

$channel = switch ($Framework) {
    'netcoreapp3.0' { '3.0' }
    'netcoreapp3.1' { '3.1' }
    'net5.0' { '5.0' }
    'net6.0' { '6.0' }
    'net7.0' { '7.0' }
    'net8.0' { '8.0' }
    'net9.0' { '9.0' }
    'net10.0' { '10.0' }
    default { $null }
}

$installScriptSuffix = if ($env:CI_JOB_ID) { $env:CI_JOB_ID } else { $PID }
$installScript = Join-Path $env:TEMP "dd-trace-dotnet-$installScriptSuffix-dotnet-install.ps1"
$installScriptUrl = 'https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1'
$dotnetExecutable = Join-Path $installDir 'dotnet.exe'
$installedRuntimes = if (Test-Path $dotnetExecutable) { & $dotnetExecutable --list-runtimes } else { @() }
$runtimePattern = if ($channel) { "^Microsoft\.NETCore\.App $([regex]::Escape($channel))\." } else { $null }
$installRuntime = $channel -and -not ($installedRuntimes -match $runtimePattern)

if (-not $installRuntime) {
    Write-Host "No additional x64 .NET runtime installation is needed for $Framework."
    exit 0
}

(New-Object System.Net.WebClient).DownloadFile($installScriptUrl, $installScript)

try {
    Write-Host "Installing the .NET $channel x64 runtime for $Framework..."
    & $installScript -Architecture x64 -Runtime dotnet -Channel $channel -InstallDir $installDir -NoPath

    # dotnet-install.ps1 can leave a stale non-zero $LASTEXITCODE from one of its
    # internal native commands even after reporting a successful installation.
    # Verify the installed shared framework directly instead.
    $installedRuntimes = & $dotnetExecutable --list-runtimes
    if ($LASTEXITCODE -ne 0 -or -not ($installedRuntimes -match $runtimePattern)) {
        throw "Failed to install the .NET $channel runtime"
    }
}
finally {
    Remove-Item $installScript -Force -ErrorAction SilentlyContinue
}
