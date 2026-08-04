param (
    [Parameter(Mandatory=$true)]
    [string]$Framework,

    [string]$InstallDir = 'C:\Program Files\dotnet'
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
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install the .NET $channel runtime"
    }
}
finally {
    Remove-Item $installScript -Force -ErrorAction SilentlyContinue
}
