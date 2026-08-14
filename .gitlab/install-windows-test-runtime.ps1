param (
    [Parameter(Mandatory=$true)]
    [string]$Framework,

    [string]$InstallDir = 'C:\Program Files\dotnet',

    [switch]$IncludeAspNetCore
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$channel = switch ($Framework) {
    'netcoreapp3.0' { '3.0' }
    'netcoreapp3.1' { '3.1' }
    'net5.0' { '5.0' }
    'net6.0' { '6.0' }
    'net7.0' { '7.0' }
    default { $null }
}

if ($null -eq $channel) {
    Write-Host ".NET runtime installation is not required for $Framework."
    exit 0
}

$installScript = Join-Path $env:TEMP 'dotnet-install.ps1'
$installScriptUrl = 'https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1'

Write-Host "Installing the .NET $channel x64 runtime for $Framework..."
(New-Object System.Net.WebClient).DownloadFile($installScriptUrl, $installScript)

try {
    & $installScript -Architecture x64 -Runtime dotnet -Channel $channel -InstallDir $InstallDir -NoPath

    # dotnet-install.ps1 can leave a stale non-zero $LASTEXITCODE from one of its
    # internal native commands even after reporting a successful installation.
    # Verify the installed shared framework directly instead.
    $installedRuntimes = & (Join-Path $InstallDir 'dotnet.exe') --list-runtimes
    $runtimePattern = "^Microsoft\.NETCore\.App $([regex]::Escape($channel))\."
    if ($LASTEXITCODE -ne 0 -or -not ($installedRuntimes -match $runtimePattern)) {
        throw "Failed to install the .NET $channel runtime"
    }

    if ($IncludeAspNetCore) {
        Write-Host "Installing the ASP.NET Core $channel x64 runtime for $Framework..."
        & $installScript -Architecture x64 -Runtime aspnetcore -Channel $channel -InstallDir $InstallDir -NoPath

        $installedRuntimes = & (Join-Path $InstallDir 'dotnet.exe') --list-runtimes
        $aspNetCorePattern = "^Microsoft\.AspNetCore\.App $([regex]::Escape($channel))\."
        if ($LASTEXITCODE -ne 0 -or -not ($installedRuntimes -match $aspNetCorePattern)) {
            throw "Failed to install the ASP.NET Core $channel runtime"
        }
    }
}
finally {
    Remove-Item $installScript -Force -ErrorAction SilentlyContinue
}
