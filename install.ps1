<#
.SYNOPSIS
    Install or uninstall the Datadog .NET APM Tracer on Windows.

.DESCRIPTION
    Downloads and installs the Datadog .NET APM Tracer MSI from GitHub releases.
    Supports installation to custom paths and silent installation by default.

.PARAMETER Version
    Specify version to install (e.g., "v3.36.0" or "3.36.0").
    Default: latest release

.PARAMETER Path
    Installation directory for the tracer.
    Default: C:\Program Files\Datadog\.NET Tracer

.PARAMETER Uninstall
    Uninstall the tracer instead of installing.

.PARAMETER Interactive
    Show MSI UI during installation (default is silent).

.PARAMETER NoCleanup
    Keep the downloaded MSI file after installation.

.EXAMPLE
    .\install.ps1
    Install the latest version to the default location.

.EXAMPLE
    .\install.ps1 -Version v3.36.0
    Install a specific version.

.EXAMPLE
    .\install.ps1 -Version 3.36.0 -Path "C:\Custom\Path"
    Install to a custom directory.

.EXAMPLE
    .\install.ps1 -Uninstall
    Uninstall the tracer.

.EXAMPLE
    .\install.ps1 -Interactive
    Install with MSI UI visible.

.EXAMPLE
    .\install.ps1 -Version v3.36.0 -WhatIf
    Show what would happen without actually installing.

.EXAMPLE
    .\install.ps1 -Uninstall -Confirm:$false
    Uninstall without confirmation prompt.

#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter()]
    [string]$Version,

    [Parameter()]
    [string]$Path = "C:\Program Files\Datadog\.NET Tracer",

    [Parameter()]
    [switch]$Uninstall,

    [Parameter()]
    [switch]$Interactive,

    [Parameter()]
    [switch]$NoCleanup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Constants
$GitHubRepo = "DataDog/dd-trace-dotnet"
$Architecture = "x64"  # Windows MSI is always x64

# Helper functions
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Warning "[WARN] $Message"
}

function Write-Err {
    param([string]$Message)
    Write-Error "[ERROR] $Message"
}

function Test-Administrator {
    <#
    .SYNOPSIS
        Check if running as administrator.
    #>
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-LatestVersion {
    <#
    .SYNOPSIS
        Fetch the latest release version from GitHub.
    #>
    Write-Info "Fetching latest release version..."

    $url = "https://api.github.com/repos/$GitHubRepo/releases/latest"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
        return $response.tag_name
    }
    catch {
        Write-Err "Failed to fetch latest version from GitHub: $_"
        throw
    }
}

function Get-NormalizedVersion {
    <#
    .SYNOPSIS
        Normalize version to ensure 'v' prefix.
    #>
    param([string]$Version)

    if ($Version -notmatch '^v') {
        return "v$Version"
    }
    return $Version
}

function Get-VersionNumber {
    <#
    .SYNOPSIS
        Strip 'v' prefix from version for artifact name.
    #>
    param([string]$Version)

    return $Version -replace '^v', ''
}

function Get-MsiDownloadUrl {
    <#
    .SYNOPSIS
        Construct the MSI download URL.
    #>
    param([string]$Version)

    $versionNumber = Get-VersionNumber -Version $Version
    $artifactName = "datadog-dotnet-apm-$versionNumber-$Architecture.msi"
    $url = "https://github.com/$GitHubRepo/releases/download/$Version/$artifactName"

    return @{
        Url = $url
        FileName = $artifactName
    }
}

function Get-DownloadedMsi {
    <#
    .SYNOPSIS
        Download the MSI installer.
    #>
    param(
        [string]$Url,
        [string]$FileName
    )

    $tempPath = Join-Path -Path $env:TEMP -ChildPath $FileName

    Write-Info "Downloading $FileName..."
    Write-Info "URL: $Url"

    try {
        # Use BITS transfer for better progress and resume capability
        if (Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue) {
            Start-BitsTransfer -Source $Url -Destination $tempPath -Description "Downloading Datadog .NET Tracer" -ErrorAction Stop
        }
        else {
            # Fallback to Invoke-WebRequest
            $ProgressPreference = 'SilentlyContinue'  # Faster download
            Invoke-WebRequest -Uri $Url -OutFile $tempPath -ErrorAction Stop
            $ProgressPreference = 'Continue'
        }

        Write-Info "Download complete: $tempPath"
        return $tempPath
    }
    catch {
        Write-Err "Failed to download MSI: $_"
        throw
    }
}

function Install-Tracer {
    <#
    .SYNOPSIS
        Install the tracer using msiexec.
    #>
    param(
        [string]$MsiPath,
        [string]$InstallPath,
        [bool]$Silent
    )

    if (-not $PSCmdlet.ShouldProcess("Datadog .NET APM Tracer", "Install to $InstallPath")) {
        return
    }

    Write-Info "Installing Datadog .NET APM Tracer..."
    Write-Info "Installation path: $InstallPath"

    # Build msiexec arguments
    $msiArgs = @(
        '/i', "`"$MsiPath`""
        "INSTALLFOLDER=`"$InstallPath`""
    )

    if ($Silent) {
        $msiArgs += '/quiet', '/norestart'
        Write-Info "Running silent installation..."
    }
    else {
        $msiArgs += '/passive', '/norestart'
        Write-Info "Running interactive installation..."
    }

    # Add logging
    $logPath = Join-Path -Path $env:TEMP -ChildPath "datadog-tracer-install.log"
    $msiArgs += '/l*v', "`"$logPath`""

    try {
        $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $msiArgs -Wait -PassThru -NoNewWindow

        if ($process.ExitCode -eq 0) {
            Write-Info "Installation completed successfully!"
        }
        elseif ($process.ExitCode -eq 3010) {
            Write-Warn "Installation completed successfully. A reboot is required."
        }
        else {
            Write-Err "Installation failed with exit code: $($process.ExitCode)"
            Write-Err "Check log file: $logPath"
            throw "MSI installation failed"
        }
    }
    catch {
        Write-Err "Failed to install MSI: $_"
        throw
    }
}

function Uninstall-Tracer {
    <#
    .SYNOPSIS
        Uninstall the tracer.
    #>
    if (-not $PSCmdlet.ShouldProcess("Datadog .NET APM Tracer", "Uninstall")) {
        return
    }

    Write-Info "Uninstalling Datadog .NET APM Tracer..."

    # Find installed product by searching for Datadog tracer
    $productName = "Datadog APM"

    try {
        $product = Get-WmiObject -Class Win32_Product -Filter "Name LIKE '%Datadog%' AND Name LIKE '%APM%'" -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($null -eq $product) {
            Write-Warn "Datadog .NET APM Tracer is not installed or could not be found."
            Write-Info "Attempting uninstall via registry..."

            # Try to find uninstall string in registry
            $uninstallKey = Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" |
                Get-ItemProperty |
                Where-Object { $_.DisplayName -like "*Datadog*" -and $_.DisplayName -like "*APM*" } |
                Select-Object -First 1

            if ($null -eq $uninstallKey) {
                $uninstallKey = Get-ChildItem "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" -ErrorAction SilentlyContinue |
                    Get-ItemProperty |
                    Where-Object { $_.DisplayName -like "*Datadog*" -and $_.DisplayName -like "*APM*" } |
                    Select-Object -First 1
            }

            if ($null -ne $uninstallKey) {
                $productCode = $uninstallKey.PSChildName

                if ($productCode -match '^\{[A-F0-9-]+\}$') {
                    Write-Info "Found product code: $productCode"

                    $logPath = Join-Path -Path $env:TEMP -ChildPath "datadog-tracer-uninstall.log"
                    $msiArgs = @('/x', $productCode, '/quiet', '/norestart', '/l*v', "`"$logPath`"")

                    $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $msiArgs -Wait -PassThru -NoNewWindow

                    if ($process.ExitCode -eq 0) {
                        Write-Info "Uninstallation completed successfully!"
                    }
                    else {
                        Write-Err "Uninstallation failed with exit code: $($process.ExitCode)"
                        Write-Err "Check log file: $logPath"
                        throw "MSI uninstallation failed"
                    }
                }
                else {
                    Write-Err "Could not determine product code for uninstallation."
                    throw
                }
            }
            else {
                Write-Err "Datadog .NET APM Tracer installation not found."
                throw
            }
        }
        else {
            Write-Info "Found installed product: $($product.Name)"
            Write-Info "Uninstalling..."

            $result = $product.Uninstall()

            if ($result.ReturnValue -eq 0) {
                Write-Info "Uninstallation completed successfully!"
            }
            else {
                Write-Err "Uninstallation failed with return code: $($result.ReturnValue)"
                throw "Uninstallation failed"
            }
        }

        Write-Host ""
        Write-Host "Uninstallation complete!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Remember to remove or unset the following environment variables:" -ForegroundColor Yellow
        Write-Host "    - COR_ENABLE_PROFILING"
        Write-Host "    - COR_PROFILER"
        Write-Host "    - COR_PROFILER_PATH"
        Write-Host "    - CORECLR_ENABLE_PROFILING"
        Write-Host "    - CORECLR_PROFILER"
        Write-Host "    - CORECLR_PROFILER_PATH"
        Write-Host "    - DD_DOTNET_TRACER_HOME"
        Write-Host ""
    }
    catch {
        Write-Err "Failed to uninstall: $_"
        throw
    }
}

function Show-EnvironmentVariables {
    <#
    .SYNOPSIS
        Display environment variables to set after installation.
    #>
    param([string]$InstallPath)

    Write-Host ""
    Write-Host "Installation successful!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To enable automatic instrumentation, set the following environment variables:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "For .NET Framework applications:" -ForegroundColor Yellow
    Write-Host "    [System.Environment]::SetEnvironmentVariable('COR_ENABLE_PROFILING', '1', 'Machine')"
    Write-Host "    [System.Environment]::SetEnvironmentVariable('COR_PROFILER', '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}', 'Machine')"
    Write-Host "    [System.Environment]::SetEnvironmentVariable('COR_PROFILER_PATH', '$InstallPath\win-x64\Datadog.Trace.ClrProfiler.Native.dll', 'Machine')"
    Write-Host ""
    Write-Host "For .NET Core/.NET 5+ applications:" -ForegroundColor Yellow
    Write-Host "    [System.Environment]::SetEnvironmentVariable('CORECLR_ENABLE_PROFILING', '1', 'Machine')"
    Write-Host "    [System.Environment]::SetEnvironmentVariable('CORECLR_PROFILER', '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}', 'Machine')"
    Write-Host "    [System.Environment]::SetEnvironmentVariable('CORECLR_PROFILER_PATH', '$InstallPath\win-x64\Datadog.Trace.ClrProfiler.Native.dll', 'Machine')"
    Write-Host ""
    Write-Host "For both:" -ForegroundColor Yellow
    Write-Host "    [System.Environment]::SetEnvironmentVariable('DD_DOTNET_TRACER_HOME', '$InstallPath', 'Machine')"
    Write-Host ""
    Write-Host "For more information, see:" -ForegroundColor Cyan
    Write-Host "    https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-framework"
    Write-Host "    https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core"
    Write-Host ""
}

# Main script
function Main {
    Write-Info "Datadog .NET APM Tracer Installer"
    Write-Info "=================================="
    Write-Host ""

    # Check for administrator privileges (skip in WhatIf mode)
    if (-not $WhatIfPreference -and -not (Test-Administrator)) {
        Write-Err "This script requires administrator privileges."
        Write-Err "Please run PowerShell as Administrator and try again."
        exit 1
    }

    # Handle uninstall mode
    if ($Uninstall) {
        Uninstall-Tracer
        return
    }

    # Normalize version
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = Get-LatestVersion
        Write-Info "Latest version: $Version"
    }
    else {
        $Version = Get-NormalizedVersion -Version $Version
        Write-Info "Installing version: $Version"
    }

    # Get download URL
    $downloadInfo = Get-MsiDownloadUrl -Version $Version

    # Download MSI
    $msiPath = Get-DownloadedMsi -Url $downloadInfo.Url -FileName $downloadInfo.FileName

    try {
        # Install
        Install-Tracer -MsiPath $msiPath -InstallPath $Path -Silent (-not $Interactive)

        # Show environment variables
        Show-EnvironmentVariables -InstallPath $Path
    }
    finally {
        # Cleanup
        if (-not $NoCleanup -and (Test-Path $msiPath)) {
            Write-Info "Cleaning up downloaded MSI..."
            Remove-Item -Path $msiPath -Force -ErrorAction SilentlyContinue
        }
    }
}

# Run main function
try {
    Main
}
catch {
    Write-Host ""
    Write-Host "Installation failed: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
