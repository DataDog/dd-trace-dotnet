param(
    [Parameter(Mandatory = $false)][switch] $Container
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ErrorActionPreference = 'Stop'

$RegRootPath = "HKLM:\SOFTWARE\DatadogDeveloper"

function Get-VersionKey() {
    param(
        [Parameter(Mandatory = $true)][string] $Component,
        [Parameter(Mandatory = $true)][string] $Keyname
    )
    $keypath = $RegRootPath + "\$($Component)"
    $value = Get-ItemPropertyValue -path $keypath -name $Keyname -ErrorAction SilentlyContinue
    if($? -eq $false){
        # key not present
        return $null
    }
    return $value
}
# returns two booleans; first is whether currently installed, second
# is whether targetvalue == current value
function Get-InstallUpgradeStatus() {
    param(
        [Parameter(Mandatory = $true)][string] $Component,
        [Parameter(Mandatory = $true)][string] $Keyname,
        [Parameter(Mandatory = $true)][string] $TargetValue
    )
    $v = Get-VersionKey -Component $Component -Keyname $Keyname
    if($null -eq $v){
        Write-Host -ForegroundColor DarkMagenta "Component $Component $Keyname not found"
        return $false, $false
    }
    if($v -eq $TargetValue){
        return $true, $true
    }
    Write-Host -ForegroundColor DarkMagenta "Component found, but $v not equal $TargetValue"
    return $true, $false
}

function Set-InstalledVersionKey() {
    param(
        [Parameter(Mandatory = $true)][string] $Component,
        [Parameter(Mandatory = $true)][string] $Keyname,
        [Parameter(Mandatory = $true)][string] $TargetValue
    )
    $keypath = $RegRootPath + "\$($Component)"
    if(!(test-path $keypath)){
        New-Item $keypath -Force
    }
    New-ItemProperty -Path $keypath -Name $Keyname -Value $TargetValue -PropertyType String -Force 
}
# Define get-remotefile so that it can be used throughout
function Get-RemoteFile() {
    param(
        [Parameter(Mandatory = $true)][string] $RemoteFile,
        [Parameter(Mandatory = $true)][string] $LocalFile,
        [Parameter(Mandatory = $false)][string] $VerifyHash
    )
    Write-Host -ForegroundColor Green "Downloading: $RemoteFile"
    Write-Host -ForegroundColor Green "         To: $LocalFile"
    (New-Object System.Net.WebClient).DownloadFile($RemoteFile, $LocalFile)
    if ($PSBoundParameters.ContainsKey("VerifyHash")){
        $dlhash = (Get-FileHash -Algorithm SHA256 $LocalFile).hash.ToLower()
        if($dlhash -ne $VerifyHash){
            Write-Host -ForegroundColor Red "Unexpected file hash downloading $LocalFile from $RemoteFile"
            Write-Host -ForegroundColor Red "Expected $VerifyHash, got $dlhash"
            throw 'Unexpected File Hash'
        }
    }
}

function Add-EnvironmentVariable() {
    param(
        [Parameter(Mandatory = $true)][string] $Variable,
        [Parameter(Mandatory = $true)][string] $Value,
        [Parameter(Mandatory = $false)][switch] $Local,
        [Parameter(Mandatory = $false)][switch] $Global
    )
    if($Local) {
        [Environment]::SetEnvironmentVariable($Variable, $Value, [System.EnvironmentVariableTarget]::Process)
    }
    if($Global){
        if($TargetContainer){
            [Environment]::SetEnvironmentVariable($Variable, $Value, [System.EnvironmentVariableTarget]::User)
        } else {
            $GlobalEnvVariables.EnvironmentVars[$($Variable)] = $Value
        }
    }
}

function Add-ToPath() {
    param(
        [Parameter(Mandatory = $true)][string] $NewPath,
        [Parameter(Mandatory = $false)][switch] $Local,
        [Parameter(Mandatory = $false)][switch] $Global
    )
    if($Local) {
        if( $NewPath -like "*python*"){
            $Env:Path="$NewPath;$Env:PATH"
        } else {
            $Env:Path="$Env:Path;$NewPath"
        }
    }
    if($Global){
        if($TargetContainer){
            $oldPath=[Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
            $target="$oldPath;$NewPath"
            [Environment]::SetEnvironmentVariable("Path", $target, [System.EnvironmentVariableTarget]::User)
        } else {
            if ($GlobalEnvVariables.PathEntries -notcontains $NewPath){
                $GlobalEnvVariables.PathEntries += $NewPath
            }
        }
    }
}

function DownloadAndExpandTo{
    param(
        [Parameter(Mandatory = $true)][string] $TargetDir,
        [Parameter(Mandatory = $true)][string] $SourceURL,
        [Parameter(Mandatory = $true)][string] $Sha256
    )
    $tmpOutFile = New-TemporaryFile

    Get-RemoteFile -LocalFile $tmpOutFile -RemoteFile $SourceURL -VerifyHash $Sha256

    If(!(Test-Path $TargetDir))
    {
        md $TargetDir
    }

    Start-Process "7z" -ArgumentList "x -o${TargetDir} $tmpOutFile" -Wait
    Remove-Item $tmpOutFile
}

function Reload-Path() {
    $newpath = @()
    $syspath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)
    $userpath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
    $existingpath = $Env:PATH
    $all = @()
    $all += $syspath -split ";"
    $all += $userpath -split ";"
    $all += $existingpath -split ";"
    foreach ($p in $all){
        if ($newpath -notcontains $p){
            $newpath += $p
        }
    }
    $Env:PATH=$newpath -join ";"
}

function Get-VariableFile {
    $targetdir = "$($Env:USERPROFILE)\.ddbuild"
    if(!(test-path $targetdir)){
        $null = New-Item -path $targetdir -ItemType Directory
    }
    
    $targetfile = "$targetdir\environment.json"
    return $targetfile
}
function Read-Variables() {
    $varfile = Get-VariableFile
    Write-Host -ForegroundColor Magenta "varfile [$varfile]"
    if(! (test-path $varfile -PathType Leaf)){
        Write-Host -ForegroundColor Yellow "$varfile does not exist"
        return
    }
    $fromfile = Get-content $varfile | ConvertFrom-Json

    $GlobalEnvVariables.PathEntries = $fromfile.PathEntries
    ## add to the local path in case we need things for adding/upgrading
    foreach ($e in $GlobalEnvVariables.PathEntries) {
        Add-ToPath -NewPath $e -Local
    }
    # need to walk the hash table manually.
    $fromfile.EnvironmentVars.psobject.properties | foreach {
        $GlobalEnvVariables.EnvironmentVars[$_.Name] = $_.Value

        ## set the variable for this shell so that anything that's required
        ## for update/upgrade is in place
        Add-EnvironmentVariable -Variable $_.Name -Value $_.Value -Local
    }
}
function Write-Variables() {
    $targetfile = Get-VariableFile
    $GlobalEnvVariables | convertto-json | set-content -path $targetfile

}