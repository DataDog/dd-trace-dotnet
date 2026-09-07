param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$false)][string]$InstallRoot = "C:\vcpkg"
)

# Installs and bootstraps vcpkg into a fixed location, then pre-fetches the helper tools it downloads
# on first use (cmake, 7zip, powershell-core, ninja). GetVcpkg() in tracer/build/_build/Build.Steps.cs
# finds vcpkg.exe on PATH, and the build no longer relocates vcpkg's downloads root, so these
# pre-fetched tools (under $InstallRoot\downloads\tools) are reused instead of downloaded on every
# build. Keep $Version in sync with the vcpkgVersion constant in Build.Steps.cs so the pre-fetched
# tool versions match the ones the build's vcpkg expects.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$out = "$($PSScriptRoot)\vcpkg.zip"
$urls = @(
    "https://github.com/microsoft/vcpkg/archive/refs/tags/$Version.zip",
    "https://codeload.github.com/microsoft/vcpkg/zip/refs/tags/$Version"
)

$downloaded = $false
foreach ($attempt in 1..4) {
    $url = $urls[($attempt - 1) % $urls.Count]
    try {
        Write-Host -ForegroundColor Green "Downloading vcpkg $Version from $url to $out (attempt $attempt of 4)"
        (New-Object System.Net.WebClient).DownloadFile($url, $out)
        $downloaded = $true
        break
    }
    catch {
        Remove-Item $out -Force -ErrorAction SilentlyContinue
        if ($attempt -eq 4) { throw }

        Write-Warning "Could not download vcpkg from $url`: $($_.Exception.Message)"
        Start-Sleep -Seconds (5 * $attempt)
    }
}

if (-not $downloaded) { throw "Could not download vcpkg $Version" }

Write-Host -ForegroundColor Green "Extracting $out"
$parent = Split-Path -Parent $InstallRoot
Expand-Archive -Path $out -DestinationPath $parent -Force
Remove-Item $out

# The archive expands to a "vcpkg-<version>" folder; rename it to the fixed install root.
if (Test-Path $InstallRoot) { Remove-Item -Recurse -Force $InstallRoot }
Rename-Item -Path (Join-Path $parent "vcpkg-$Version") -NewName (Split-Path -Leaf $InstallRoot)

Write-Host -ForegroundColor Green "Bootstrapping vcpkg"
& "$InstallRoot\bootstrap-vcpkg.bat" -disableMetrics
if ($LASTEXITCODE -ne 0) { throw "bootstrap-vcpkg.bat failed with exit code $LASTEXITCODE" }

# Add vcpkg to the machine PATH so it is resolved by ToolPathResolver.GetPathExecutable at build time.
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine) + ";$InstallRoot", [System.EnvironmentVariableTarget]::Machine)

# Pre-fetch the helper tools vcpkg would otherwise download on first use. These land under
# $InstallRoot\downloads\tools, which is the default downloads root the build uses now that it no
# longer overrides --downloads-root.
foreach ($tool in @('git', 'cmake', '7zip', 'powershell-core', 'ninja')) {
    Write-Host -ForegroundColor Green "Pre-fetching vcpkg tool: $tool"
    & "$InstallRoot\vcpkg.exe" fetch $tool
    if ($LASTEXITCODE -ne 0) { throw "vcpkg fetch $tool failed with exit code $LASTEXITCODE" }
}

Write-Host -ForegroundColor Green "Installed vcpkg $Version to $InstallRoot"
& "$InstallRoot\vcpkg.exe" version
