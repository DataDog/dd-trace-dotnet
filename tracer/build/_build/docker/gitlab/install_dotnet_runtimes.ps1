param (
    [string]$InstallDir = "C:\Program Files\dotnet"
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$installScript = Join-Path $PSScriptRoot 'dotnet-install.ps1'
$installScriptUrl = 'https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1'

(New-Object System.Net.WebClient).DownloadFile($installScriptUrl, $installScript)

try {
    foreach ($channel in @('3.0', '3.1', '5.0', '6.0', '7.0')) {
        & $installScript -Architecture x64 -Runtime dotnet -Channel $channel -InstallDir $InstallDir -NoPath
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install the .NET $channel runtime"
        }
    }
}
finally {
    Remove-Item $installScript -Force -ErrorAction SilentlyContinue
}

dotnet --list-runtimes
