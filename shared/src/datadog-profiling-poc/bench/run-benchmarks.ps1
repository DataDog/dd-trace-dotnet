<#
.SYNOPSIS
Benchmarks the PoC C library against a given libdatadog release (Windows, no CMake).

.DESCRIPTION
Downloads the libdatadog Windows release artifact, compiles the PoC sources
of this checkout directly with MSVC (WinHTTP exporter, the vendored zstd
amalgamation from vendor\zstd - same files as the profiler's .vcxproj), builds
bench.c against both,
runs the scenarios and writes <Out>\report.md and results.json.

Requirements: Visual Studio 2019+ (or Build Tools) with the C++ workload,
Python 3 (python or py on PATH), PowerShell 5.1+.

.EXAMPLE
.\run-benchmarks.ps1 -LibdatadogVersion v44.0.0
.\run-benchmarks.ps1 -LibdatadogVersion v38.0.0 -Arch x86 -Quick
#>
param(
    [string]$LibdatadogVersion = "v38.0.0",
    [ValidateSet("x64", "x86")][string]$Arch = "x64",
    [string]$Out = (Join-Path $PSScriptRoot "out"),
    [string]$Scenarios = "all",
    [int]$Cpu = 2,
    [int]$Repeat = 3,
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue" # Invoke-WebRequest is very slow with the progress bar
if (-not $LibdatadogVersion.StartsWith("v")) { $LibdatadogVersion = "v$LibdatadogVersion" }
$PocDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$Cache = Join-Path $Out "cache"
New-Item -ItemType Directory -Force -Path $Cache | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-Download([string]$Url, [string]$Path) {
    if (Test-Path $Path) { return $true }
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile "$Path.part"
        Move-Item "$Path.part" $Path
        return $true
    } catch {
        Remove-Item -ErrorAction SilentlyContinue "$Path.part"
        return $false
    }
}

# Extracts the zip entries whose path starts with one of $Prefixes into $Dest.
function Expand-Entries([string]$Zip, [string]$Dest, [string[]]$Prefixes) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ($name.EndsWith('/')) { continue }
            if (-not ($Prefixes | Where-Object { $name.StartsWith($_) })) { continue }
            $target = Join-Path $Dest $name
            New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $archive.Dispose() }
}

# ---- Python ----
$Python = $null
foreach ($candidate in @("python", "py")) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) { $Python = $candidate; break }
}
if (-not $Python) { throw "Python 3 not found on PATH (python or py)" }

# ---- MSVC environment ----
if (-not (Get-Command cl.exe -ErrorAction SilentlyContinue)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { throw "vswhere.exe not found: install Visual Studio or the Build Tools with the C++ workload" }
    $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vs) { throw "no Visual Studio installation with the C++ tools (Microsoft.VisualStudio.Component.VC.Tools.x86.x64)" }
    $vcvars = Join-Path $vs "VC\Auxiliary\Build\vcvarsall.bat"
    Write-Host "==> using MSVC from $vs ($Arch)"
    # import the environment set by vcvarsall into this PowerShell session
    $envDump = & cmd.exe /c "call `"$vcvars`" $Arch && set" 2>&1
    foreach ($line in $envDump) {
        if ($line -match '^([^=]+)=(.*)$') { Set-Item -Path "env:$($Matches[1])" -Value $Matches[2] }
    }
    if (-not (Get-Command cl.exe -ErrorAction SilentlyContinue)) { throw "cl.exe still not found after vcvarsall $Arch" }
}

# ---- libdatadog release artifact ----
$Asset = "libdatadog-$Arch-windows.zip"
$LddZip = Join-Path $Cache "libdatadog-$LibdatadogVersion-$Arch-windows.zip"
$LddRoot = Join-Path $Cache "libdatadog-$LibdatadogVersion-$Arch"
if (-not (Test-Path $LddRoot)) {
    Write-Host "==> downloading libdatadog $LibdatadogVersion ($Asset, ~250 MB)"
    if (-not (Invoke-Download "https://github.com/DataDog/libdatadog/releases/download/$LibdatadogVersion/$Asset" $LddZip)) {
        throw "no '$Asset' in libdatadog release $LibdatadogVersion. Patch releases (e.g. v44.0.2) are published without binaries; use the .0 release (e.g. v44.0.0)."
    }
    $top = "libdatadog-$Arch-windows/"
    Expand-Entries $LddZip "$LddRoot.tmp" @("${top}include/", "${top}release/dynamic/")
    Move-Item "$LddRoot.tmp" $LddRoot
    Remove-Item $LddZip # only the headers and the release DLL are kept
}
$LddDir = Join-Path $LddRoot "libdatadog-$Arch-windows"
$LddDll = Join-Path $LddDir "release\dynamic\datadog_profiling_ffi.dll"
$LddLib = Join-Path $LddDir "release\dynamic\datadog_profiling_ffi.lib"
if (-not (Test-Path $LddDll) -or -not (Test-Path $LddLib)) { throw "datadog_profiling_ffi.dll/.lib not found under $LddDir" }

# ---- build ----
Write-Host "==> building the benchmarks"
$Obj = Join-Path $Out "obj-$Arch"
foreach ($d in @("poc", "bench_poc", "bench_ldd")) { New-Item -ItemType Directory -Force -Path (Join-Path $Obj $d) | Out-Null }
$CFlags = @("/nologo", "/O2", "/std:c11", "/W3", "/D_CRT_SECURE_NO_WARNINGS", "/MD")
# same files as Datadog.Profiler.Native.vcxproj: the PoC sources (WinHTTP exporter, not
# the libcurl one) and the vendored zstd single-file amalgamation
$ZstdDir = Join-Path $PocDir "vendor\zstd"
$PocSources = @(Get-ChildItem (Join-Path $PocDir "src\*.c") | Where-Object { $_.Name -ne "exporter.c" } | ForEach-Object { $_.FullName }) +
              @(Join-Path $ZstdDir "zstd.c")

function Invoke-Cl([string[]]$Arguments, [string]$What) {
    $log = Join-Path $Out "build-$What.log"
    & cl.exe @Arguments *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content $log -Tail 30; throw "build of $What failed (see $log)" }
}

Invoke-Cl ($CFlags + @("/c", "/I$PocDir\include", "/I$PocDir\src", "/I$ZstdDir") + $PocSources + @("/Fo$Obj\poc\")) "poc"
$PocObjs = Get-ChildItem "$Obj\poc\*.obj" | ForEach-Object { $_.FullName }
$BenchPoc = Join-Path $Out "bench_poc.exe"
$BenchLdd = Join-Path $Out "bench_ldd.exe"
Invoke-Cl ($CFlags + @("/DBACKEND_POC", "/I$PocDir\include", (Join-Path $PSScriptRoot "bench.c")) + $PocObjs +
           @("/Fo$Obj\bench_poc\", "/Fe$BenchPoc", "/link", "winhttp.lib", "psapi.lib")) "bench_poc"
try {
    Invoke-Cl ($CFlags + @("/DBACKEND_LDD", "/I$LddDir\include", (Join-Path $PSScriptRoot "bench.c"),
               "/Fo$Obj\bench_ldd\", "/Fe$BenchLdd", "/link", $LddLib, "psapi.lib")) "bench_ldd"
} catch {
    throw "bench.c does not compile against libdatadog $LibdatadogVersion's FFI. It targets the v38-style API (verified with v38.0.0 and v44.0.0); adapt the BACKEND_LDD section of bench.c. ($_)"
}
Copy-Item -Force $LddDll $Out # next to bench_ldd.exe

# ---- run ----
$PocVersion = (& git -C $PocDir describe --always --dirty 2>$null)
if (-not $PocVersion) { $PocVersion = "unknown" }
$RunArgs = @((Join-Path $PSScriptRoot "run.py"), "--poc", $BenchPoc, "--ldd", $BenchLdd, "--out", $Out,
             "--scenarios", $Scenarios, "--repeat", "$Repeat", "--cpu", "$Cpu",
             "--ldd-version", "$LibdatadogVersion ($Arch)", "--poc-version", "$PocVersion")
if ($Quick) { $RunArgs += "--quick" }
if ($Python -eq "py") { $RunArgs = @("-3") + $RunArgs }
Write-Host "==> running (this takes a few minutes; progress below)"
& $Python @RunArgs
if ($LASTEXITCODE -ne 0) { throw "benchmark run failed" }
Write-Host "==> report: $(Join-Path $Out 'report.md')"
