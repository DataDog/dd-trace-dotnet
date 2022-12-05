#
# Installs .NET Runtime 3.5, used by Wix 3.11 and the Visual C++ Compiler for Python 2.7
#
$ErrorActionPreference = "Stop"

$UpgradeTable = @{
    1809 = @{
    ## Taken from the mcr.microsoft.com/dotnet/framework/runtime:3.5 Dockerfile:
    ## https://github.com/microsoft/dotnet-framework-docker/blob/26597e42d157cc1e09d1e0dc8f23c32e6c3d1467/3.5/runtime/windowsservercore-ltsc2019/Dockerfile

        netfxzip = "https://dotnetbinaries.blob.core.windows.net/dockerassets/microsoft-windows-netfx3-1809.zip";
        netfxsha256 = "439f0b98438d9d302a21765cdfe2f28af784aa6a63dfff11c4e1a8a20e9fdf93"
        # Trying to get the patch with HTTPS yields:
        # 2022-02-24T15:53:44.6586746+00:00: curl: (60) schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.
        patch = "http://download.windowsupdate.com/c/msdownload/update/software/updt/2020/01/windows10.0-kb4534119-x64_a2dce2c83c58ea57145e9069f403d4a5d4f98713.msu";
        patchsha256 = "0287ba4106ba5462ba542e0124c4211f50bbce1123167375f125ea27b8f48632"
        expandedpatch = "windows10.0-kb4534119-x64.cab"
    };
    1909 = @{
    ## Taken from the mcr.microsoft.com/dotnet/framework/runtime:3.5 Dockerfile:
    ## https://github.com/microsoft/dotnet-framework-docker/blob/abc2ca65b28058f7c71ec8cd8763a8fbf2a9c03f/3.5/runtime/windowsservercore-1909/Dockerfile

        netfxzip = "https://dotnetbinaries.blob.core.windows.net/dockerassets/microsoft-windows-netfx3-1909.zip";
        netfxsha256 = "e451de8d0de5b6e8c6275e7f5baeec0fe755238964a42b89602f5e7a57542de2"
        # Trying to get the patch with HTTPS yields:
        # 2022-02-24T15:53:44.6586746+00:00: curl: (60) schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.
        patch = "http://download.windowsupdate.com/d/msdownload/update/software/updt/2020/01/windows10.0-kb4534132-x64-ndp48_21067bd5f9c305ee6a6cee79db6ca38587cb6ad8.msu";
        patchsha256 = "6f8acc2f1d9d83230ec400a04c5de07899dbf1ab3c8b299b0cea973c18abe009"
        expandedpatch = "windows10.0-kb4534132-x64-ndp48.cab"
    };
    2004 = @{
    # Taken from the mcr.microsoft.com/dotnet/framework/runtime:3.5 Dockerfile:
    # https://github.com/microsoft/dotnet-framework-docker/blob/25bdac46765c6dae7d05994cace836303f63b5e3/src/runtime/3.5/windowsservercore-2004/Dockerfile
        netfxzip = "https://dotnetbinaries.blob.core.windows.net/dockerassets/microsoft-windows-netfx3-2004.zip";
        netfxsha256 = "06ce7df5864490c76da45bd5d83662810afdbd44c719b710166e9eaf8ddc73e2"
        # Trying to get the patch with HTTPS yields:
        # 2022-02-24T15:53:44.6586746+00:00: curl: (60) schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.
        patch = "http://download.windowsupdate.com/c/msdownload/update/software/updt/2020/10/windows10.0-kb4580419-x64-ndp48_197efbd77177abe76a587359cea77bda5398c594.msu";
        patchsha256 = "5890767a66b8f979378128c70b1caa1a20e61c31176f8cbf598d7ef424f7b2d2"
        expandedpatch = "Windows10.0-KB4580419-x64-NDP48.cab"
    }
    2009 = @{ ## 20H2 reports itself as 2009 from the registry
    # Taken from the mcr.microsoft.com/dotnet/framework/runtime:3.5 Dockerfile:
    # https://github.com/microsoft/dotnet-framework-docker/blob/e0b59f4aeb47bd6bf13e4c7ec6676a1935306df9/src/runtime/3.5/windowsservercore-20H2/Dockerfile
        netfxzip = "https://dotnetbinaries.blob.core.windows.net/dockerassets/microsoft-windows-netfx3-2009.zip";
        netfxsha256 = "396ad6808a3d6013ff86e0e0163a8602da3fc7ddf36b82b2b18792f6497b88f3"
        # Trying to get the patch with HTTPS yields:
        # 2022-02-24T15:53:44.6586746+00:00: curl: (60) schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.
        patch = "http://download.windowsupdate.com/c/msdownload/update/software/updt/2020/10/windows10.0-kb4580419-x64-ndp48_197efbd77177abe76a587359cea77bda5398c594.msu";
        patchsha256 = "5890767a66b8f979378128c70b1caa1a20e61c31176f8cbf598d7ef424f7b2d2"
        expandedpatch = "Windows10.0-KB4580419-x64-NDP48.cab"
    }
    2022 = @{
    # Taken from the mcr.microsoft.com/dotnet/framework/runtime:3.5 Dockerfile:
    # https://github.com/microsoft/dotnet-framework-docker/blob/ee63b25718f434065cd9d1010580ba2a7ab2119f/src/runtime/3.5/windowsservercore-ltsc2022/Dockerfile
        netfxzip = "https://dotnetbinaries.blob.core.windows.net/dockerassets/microsoft-windows-netfx3-ltsc2022.zip";
        netfxsha256 = "e7bb9d923dd9e9d11629fab173ad78ecd622c3ab669344e421db62dfac93d16b"
        # Trying to get the patch with HTTPS yields:
        # 2022-02-24T15:53:44.6586746+00:00: curl: (60) schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.
        patch = "http://download.windowsupdate.com/c/msdownload/update/software/updt/2021/08/windows10.0-kb5005538-x64-ndp48_451a4214d51de628ef2c3c8a69c87826b2ab43c8.msu";
        patchsha256 = "d752801b38de2cd6e442385d0a2c9983a5bbe7ed87225bbb3190cdbdc1c0f067"
        expandedpatch = "windows10.0-kb5005538-x64-ndp48.cab"
    }
}
$kernelver = [int](get-itemproperty -path "hklm:software\microsoft\windows nt\currentversion" -name releaseid).releaseid
$build = [System.Environment]::OSVersion.version.build
$productname = (get-itemproperty -path "hklm:software\microsoft\windows nt\currentversion" -n productname).productname
Write-Host -ForegroundColor Green "Detected kernel version $kernelver, build $build and product name $productname"
# Windows Server 2022 still reports 2009 as releaseid
if ($build -ge 20348) {
    $kernelver = 2022
}

$Env:DOTNET_RUNNING_IN_CONTAINER="true"

$out = "microsoft-windows-netfx3.zip"
$sha256 = $UpgradeTable[$kernelver]["netfxsha256"]
Write-Host curl -fSLo $out $UpgradeTable[$kernelver]["netfxzip"]
curl.exe -fSLo $out $UpgradeTable[$kernelver]["netfxzip"]
if ((Get-FileHash -Algorithm SHA256 $out).Hash -ne "$sha256") { Write-Host \"Wrong hashsum for ${out}: got '$((Get-FileHash -Algorithm SHA256 $out).Hash)', expected '$sha256'.\"; exit 1 }

tar -zxf $out
remove-item -force $out
DISM /Online /Quiet /Add-Package /PackagePath:.\microsoft-windows-netfx3-ondemand-package~31bf3856ad364e35~amd64~~.cab
remove-item microsoft-windows-netfx3-ondemand-package~31bf3856ad364e35~amd64~~.cab
Remove-Item -Force -Recurse ${Env:TEMP}\* -ErrorAction SilentlyContinue


$out = "patch.msu"
$sha256 = $UpgradeTable[$kernelver]["patchsha256"]
Write-Host curl.exe -fSLo $out $UpgradeTable[$kernelver]["patch"]
curl.exe -fSLo $out $UpgradeTable[$kernelver]["patch"]
if ((Get-FileHash -Algorithm SHA256 $out).Hash -ne "$sha256") { Write-Host \"Wrong hashsum for ${out}: got '$((Get-FileHash -Algorithm SHA256 $out).Hash)', expected '$sha256'.\"; exit 1 }

mkdir patch
expand patch.msu patch -F:*
remove-item -force patch.msu
Write-Host DISM /Online /Quiet /Add-Package /PackagePath:C:\patch\$($UpgradeTable[$kernelver]["expandedpatch"])
DISM /Online /Quiet /Add-Package /PackagePath:C:\patch\$($UpgradeTable[$kernelver]["expandedpatch"])
remove-item -force -recurse patch
