[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

Write-Output "PowerShell $($PSVersionTable.PSEdition) version $($PSVersionTable.PSVersion)"

Set-StrictMode -Version 2.0; $ErrorActionPreference = "Stop"; $ConfirmPreference = "None"; trap { Write-Error $_ -ErrorAction Continue; exit 1 }
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent

###########################################################################
# CONFIGURATION
###########################################################################

$BuildProjectFile = "$PSScriptRoot\build\_build\_build.csproj"

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:NUKE_TELEMETRY_OPTOUT = 1
$env:DOTNET_NOLOGO = 1

###########################################################################
# >>> BEGIN TEMPORARY .NET 10 INSTALLATION LOGIC <<<
# Can be removed once .NET 10 SDK is preinstalled
###########################################################################

$env:DOTNET_ROLL_FORWARD_TO_PRERELEASE = 1
$env:DOTNET_CLI_UI_LANGUAGE = "en"

$dotnetVersion = "10.0.100-preview.5.25277.114"
$installDir = "$PSScriptRoot\.dotnet"

if (-not (Test-Path "$installDir\dotnet.exe")) {
    Write-Output "Installing .NET SDK $dotnetVersion to $installDir..."

    $dotnetInstallScript = "$PSScriptRoot\dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstallScript

    & powershell -NoProfile -ExecutionPolicy Bypass -File $dotnetInstallScript `
        -Version $dotnetVersion `
        -InstallDir $installDir `
        -NoPath

    Remove-Item $dotnetInstallScript -Force
	
	Write-Output ".NET SDK has been installed."
} else {
    Write-Output ".NET SDK already installed at $installDir"
}

$env:DOTNET_ROOT = $installDir
$env:PATH = "$installDir;$env:PATH"

Write-Output "Paths set."

###########################################################################
# <<< END TEMPORARY .NET 10 INSTALLATION LOGIC <<<
###########################################################################

# Allow running Nuke with the .NET 8 runtime
$env:DOTNET_ROLL_FORWARD_TO_PRERELEASE=1
###########################################################################
# EXECUTION
###########################################################################

function ExecSafe([scriptblock] $cmd) {
    & $cmd
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

# If dotnet CLI is installed globally and it matches requested version, use for execution
#TODO: Uncomment after updating VMS
$env:DOTNET_EXE = (Get-Command "dotnet").Path

# Some commands apparently break unless this is set
# e.g. "/property:Platform=AnyCPU" gives
# No se reconoce el comando o el argumento "/property:Platform=AnyCPU"
$env:DOTNET_CLI_UI_LANGUAGE="en"

Write-Output "Microsoft (R) .NET SDK version $(& $env:DOTNET_EXE --version)"

ExecSafe { & $env:DOTNET_EXE build $BuildProjectFile /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity quiet }
ExecSafe { & $env:DOTNET_EXE run --project $BuildProjectFile --no-build -- $BuildArguments }
