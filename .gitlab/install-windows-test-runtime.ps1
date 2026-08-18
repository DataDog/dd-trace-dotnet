param (
    [Parameter(Mandatory=$true)]
    [string]$Framework,

    [ValidateSet('x64', 'x86')]
    [string]$Architecture = 'x64',

    [string]$InstallDir,

    [switch]$IncludeAspNetCore
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not $InstallDir) {
    $InstallDir = if ($Architecture -eq 'x86') { 'C:\Program Files (x86)\dotnet' } else { 'C:\Program Files\dotnet' }
}

$channel = switch ($Framework) {
    'netcoreapp3.0' { '3.0' }
    'netcoreapp3.1' { '3.1' }
    'net5.0' { '5.0' }
    'net6.0' { '6.0' }
    'net7.0' { '7.0' }
    'net8.0' { if ($Architecture -eq 'x86') { '8.0' } else { $null } }
    'net9.0' { if ($Architecture -eq 'x86') { '9.0' } else { $null } }
    'net10.0' { if ($Architecture -eq 'x86') { '10.0' } else { $null } }
    default { $null }
}

$installScript = Join-Path $env:TEMP 'dotnet-install.ps1'
$installScriptUrl = 'https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1'
$dotnetExecutable = Join-Path $InstallDir 'dotnet.exe'
$globalJsonPath = Join-Path $PSScriptRoot '..\global.json'
$sdkVersion = (Get-Content $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
$installedSdks = if (Test-Path $dotnetExecutable) { & $dotnetExecutable --list-sdks } else { @() }
$installedRuntimes = if (Test-Path $dotnetExecutable) { & $dotnetExecutable --list-runtimes } else { @() }
$sdkPattern = "^$([regex]::Escape($sdkVersion)) \["
$runtimePattern = if ($channel) { "^Microsoft\.NETCore\.App $([regex]::Escape($channel))\." } else { $null }
$aspNetCorePattern = if ($channel) { "^Microsoft\.AspNetCore\.App $([regex]::Escape($channel))\." } else { $null }
$installSdk = $Architecture -eq 'x86' -and -not ($installedSdks -match $sdkPattern)
$installRuntime = $channel -and -not ($installedRuntimes -match $runtimePattern)
$installAspNetCore = $channel -and $IncludeAspNetCore -and -not ($installedRuntimes -match $aspNetCorePattern)

if (-not $installSdk -and -not $installRuntime -and -not $installAspNetCore) {
    Write-Host "The required .NET $Architecture SDK and runtimes are already installed for $Framework."
    exit 0
}

(New-Object System.Net.WebClient).DownloadFile($installScriptUrl, $installScript)

try {
    if ($installSdk) {
        Write-Host "Installing the .NET $sdkVersion $Architecture SDK..."
        & $installScript -Architecture $Architecture -Version $sdkVersion -InstallDir $InstallDir -NoPath

        $installedSdks = & $dotnetExecutable --list-sdks
        if ($LASTEXITCODE -ne 0 -or -not ($installedSdks -match $sdkPattern)) {
            throw "Failed to install the .NET $sdkVersion $Architecture SDK"
        }
    }

    if ($installRuntime) {
        Write-Host "Installing the .NET $channel $Architecture runtime for $Framework..."
        & $installScript -Architecture $Architecture -Runtime dotnet -Channel $channel -InstallDir $InstallDir -NoPath

        # dotnet-install.ps1 can leave a stale non-zero $LASTEXITCODE from one of its
        # internal native commands even after reporting a successful installation.
        # Verify the installed shared framework directly instead.
        $installedRuntimes = & $dotnetExecutable --list-runtimes
        if ($LASTEXITCODE -ne 0 -or -not ($installedRuntimes -match $runtimePattern)) {
            throw "Failed to install the .NET $channel runtime"
        }
    }

    if ($installAspNetCore) {
        Write-Host "Installing the ASP.NET Core $channel $Architecture runtime for $Framework..."
        & $installScript -Architecture $Architecture -Runtime aspnetcore -Channel $channel -InstallDir $InstallDir -NoPath

        $installedRuntimes = & $dotnetExecutable --list-runtimes
        if ($LASTEXITCODE -ne 0 -or -not ($installedRuntimes -match $aspNetCorePattern)) {
            throw "Failed to install the ASP.NET Core $channel runtime"
        }
    }
}
finally {
    Remove-Item $installScript -Force -ErrorAction SilentlyContinue
}
