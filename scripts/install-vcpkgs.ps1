$solutionRoot = [IO.Path]::GetFullPath(([IO.Path]::Combine($PSScriptRoot, "..")))
$workspaceRoot = [IO.Path]::GetFullPath(([IO.Path]::Combine($solutionRoot, "..")))
$vcpkgRoot = [IO.Path]::Combine($workspaceRoot, "vcpkg")
if ($IsWindows) {
    $vcpkgExe = [IO.Path]::Combine($vcpkgRoot, "vcpkg.exe")
} else {
    $vcpkgExe = [IO.Path]::Combine($vcpkgRoot, "vcpkg")
}


function Run {
    param(
        [Parameter(Position=0)][string] $Name,
        [Parameter(Position=1)][String[]] $Args,
        [Parameter(Position=2)][string] $WorkingDirectory
    )
    Write-Host "running $($Name) $($Args)"
    $proc = Start-Process $Name -ArgumentList $Args -WorkingDirectory $WorkingDirectory -NoNewWindow -PassThru
    $handle = $proc.Handle # cache proc.Handle
    $proc.WaitForExit(1000 * 60 * 10)
    return $proc
}


if (Test-Path $vcpkgRoot) {
    Write-Host "vcpkg repo found"
} else {
    Write-Host "vcpkg repo not found, cloning"
    $p = Run "git" "clone","https://github.com/Microsoft/vcpkg.git" $workspaceRoot
    if ($p.ExitCode -ne 0) {
        Write-Host "failed to clone vcpkg repo: $($p.ExitCode)"
        Exit 1
    }
}

if (Test-Path $vcpkgExe) {
    Write-Host "vcpkg.exe found"
} else {
    Write-Host "vcpkg.exe not found, bootstrapping"
    if ($IsWindows) {
        $p = Run "cmd" "/c","bootstrap-vcpkg.bat" $vcpkgRoot
        if ($p.ExitCode -ne 0) {
            Write-Host "failed to bootstrap vcpkg: $($p.ExitCode)"
            Exit 1
        }
    } else {
        $p = Run "sh" "bootstrap-vcpkg.sh" $vcpkgRoot
        if ($p.ExitCode -ne 0) {
            Write-Host "failed to bootstrap vcpkg: $($p.ExitCode)"
            Exit 1
        }

    }
}

$packages = @("fmt", "spdlog", "nlohmann-json")
if ($IsWindows) {
    $platforms = @("x86", "x64")
} else {
    $platforms = @("x64")
}
if ($IsWindows) {
    $os = "windows-static"
} elseif ($IsMacOS) {
    $os = "osx"
} else {
    $os = "linux"
}

foreach ($platform in $platforms) {
    foreach ($package in $packages) {
        Write-Host "installing $($package):$($platform)-$($os)"
        $p = Run $vcpkgExe "install","$($package):$($platform)-$($os)" $vcpkgRoot
        if ($p.ExitCode -ne 0) {
            Write-Host "failed to install $($package):$($platform): $($p.ExitCode)"
            Exit 1
        }
    }
}

Exit 0
