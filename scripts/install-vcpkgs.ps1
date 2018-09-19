$workspaceRoot = [IO.Path]::GetFullPath(([IO.Path]::Combine($PSScriptRoot, "..", "..")))
$vcpkgRoot = [IO.Path]::Combine($workspaceRoot, "vcpkg")
$vcpkgExe = [IO.Path]::Combine($vcpkgRoot, "vcpkg.exe")


if (Test-Path $vcpkgRoot) {
    Write-Output "vcpkg repo found"
} else {
    Write-Output "vcpkg repo not found, cloning"
    $p = Start-Process "git" `
        -ArgumentList "clone","https://github.com/Microsoft/vcpkg.git" `
        -WorkingDirectory $workspaceRoot `
        -NoNewWindow -Wait -PassThru     
    if ($p.ExitCode -ne 0) {
        Exit $p.ExitCode
    }
}

if (Test-Path $vcpkgExe) {
    Write-Output "vcpkg.exe found"
} else {
    Write-Output "vcpkg.exe not found, bootstrapping"
    $p = Start-Process "cmd" `
        -ArgumentList "/c","bootstrap-vcpkg.bat" `
        -WorkingDirectory $vcpkgRoot `
        -NoNewWindow -Wait -PassThru     
    if ($p.ExitCode -ne 0) {
        Exit $p.ExitCode
    }
}

$packages = @("spdlog", "nlohmann-json")
$platforms = @("x86", "x64")

foreach ($platform in $platforms) {
    foreach ($package in $packages) {
        Write-Output "installing $($package):$($platform)-windows-static"
        $p = Start-Process $vcpkgExe `
            -ArgumentList "install","$($package):$($platform)-windows-static" `
            -WorkingDirectory $vcpkgRoot `
            -NoNewWindow -Wait -PassThru 
        if ($p.ExitCode -ne 0) {
            Exit $p.ExitCode
        }
    }
}



Exit 0