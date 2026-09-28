param (
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$Sha256,
    [Parameter(Mandatory=$true)][string]$Url,
    [Parameter(Mandatory=$false)][string]$InstallRoot="c:\devtools\vstudio",
    [Parameter(Mandatory=$false)][switch]$NoQuiet
)

# Enabled TLS12
$ErrorActionPreference = 'Stop'

# Script directory is $PSScriptRoot

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host -ForegroundColor Green "Installing Visual Studio $($Version) from $($Url)"

$out = "$($PSScriptRoot)\vs_buildtools.exe"

Write-Host -ForegroundColor Green Downloading $Url to $out
(New-Object System.Net.WebClient).DownloadFile($Url, $out)
if ((Get-FileHash -Algorithm SHA256 $out).Hash -ne "$Sha256") { Write-Host \"Wrong hashsum for ${out}: got '$((Get-FileHash -Algorithm SHA256 $out).Hash)', expected '$Sha256'.\"; exit 1 }

# write file size to make sure it worked
Write-Host -ForegroundColor Green "File size is $((get-item $out).length)"

$VSPackages = @(
    "Microsoft.VisualStudio.Workload.ManagedDesktop",
    "Microsoft.VisualStudio.Workload.NetCoreTools",
    "Microsoft.VisualStudio.Workload.NativeDesktop",
    "Microsoft.VisualStudio.Workload.WebBuildTools",
    "Microsoft.NetCore.Component.SDK",
    "Microsoft.Net.Component.4.7.TargetingPack",
    "Microsoft.Net.Component.4.5.TargetingPack",
    "Microsoft.Net.Component.4.6.1.TargetingPack",
    "Microsoft.Net.Component.4.6.2.TargetingPack",
    "Microsoft.Net.Component.4.8.SDK",
    "Microsoft.VisualStudio.Component.FSharp",
    "Microsoft.VisualStudio.Component.FSharp.WebTemplates",
    "Microsoft.VisualStudio.ComponentGroup.NativeDesktop.Win81",
    "Microsoft.VisualStudio.Workload.VCTools",
    "Microsoft.VisualStudio.Component.VC.ATL",
    "Microsoft.VisualStudio.Component.VC.v141.ATL",
    "Microsoft.VisualStudio.Component.VC.14.29.16.11.x86.x64",
    "Microsoft.VisualStudio.Component.VC.14.29.16.11.ATL",
    "Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre",
    "Microsoft.VisualStudio.ComponentGroup.NativeDesktop.Win81"
)

$VSPackageListParam = $VSPackages -join " --add "
$ArgList = "--wait --norestart --nocache --installPath `"$($InstallRoot)`" --add $VSPackageListParam"
if(-not $NoQuiet){
    $ArgList = "--quiet $ArgList"
}
$processparams = @{
    FilePath = $out
    NoNewWindow = $true
    Wait = $true
    ArgumentList = $ArgList
}
Start-Process @processparams


setx VSTUDIO_ROOT "$InstallRoot"
[Environment]::SetEnvironmentVariable("VSTUDIO_ROOT", "$InstallRoot", [System.EnvironmentVariableTarget]::Machine)

Remove-Item $out
Write-Host -ForegroundColor Green Done with Visual Studio

# Install the required Windows SDKs that no longer ship with the VS bootstrapper
# Installer URLs come from https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads-archive
# Get the SHA256 for each with: (Get-FileHash -Algorithm SHA256 $out).Hash
$WindowsSdks = @(
    @{
        Version = "10.0.19041.0"
        Url     = "https://download.microsoft.com/download/1/c/3/1c3d5161-d9e9-4e4b-9b43-b70fe8be268c/windowssdk/winsdksetup.exe"
        Sha256  = "D53F651370F87484B78622E30DFB1A41920B501E4041035771C0D785561F47D5"
    }
)

foreach ($sdk in $WindowsSdks) {
    $sdkOut = "$($PSScriptRoot)\winsdksetup-$($sdk.Version).exe"

    Write-Host -ForegroundColor Green "Installing Windows SDK $($sdk.Version) from $($sdk.Url)"
    (New-Object System.Net.WebClient).DownloadFile($sdk.Url, $sdkOut)

    $actualHash = (Get-FileHash -Algorithm SHA256 $sdkOut).Hash
    if ([string]::IsNullOrEmpty($sdk.Sha256)) {
        Write-Host -ForegroundColor Yellow "No expected hash set for Windows SDK $($sdk.Version) - downloaded file hash is '$actualHash'. Pin this in `$WindowsSdks."
    } elseif ($actualHash -ne $sdk.Sha256) {
        Write-Host -ForegroundColor Red "Wrong hashsum for ${sdkOut}: got '$actualHash', expected '$($sdk.Sha256)'."
        exit 1
    }

    # OptionId.DesktopCPPx64/x86 are the headers/libs the native builds need; OptionId.SigningTools
    # provides signtool.exe (see the PATH entry added below for 19041's signtool).
    $sdkProcessParams = @{
        FilePath     = $sdkOut
        NoNewWindow  = $true
        Wait         = $true
        PassThru     = $true
        ArgumentList = "/features OptionId.DesktopCPPx64 OptionId.DesktopCPPx86 OptionId.SigningTools /quiet /norestart /ceip off"
    }
    $sdkProcess = Start-Process @sdkProcessParams
    if ($sdkProcess.ExitCode -ne 0) {
        Write-Host -ForegroundColor Red "Windows SDK $($sdk.Version) installer exited with code $($sdkProcess.ExitCode)"
        exit 1
    }

    Remove-Item $sdkOut
}

Write-Host -ForegroundColor Green Done installing pinned Windows SDK versions

# add SDK added above to path for signtool
# C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine) + ";${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.19041.0\x64", [System.EnvironmentVariableTarget]::Machine)
