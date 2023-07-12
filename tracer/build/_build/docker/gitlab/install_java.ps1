$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$TargetContainer = $true
. "$($PSScriptRoot)\helpers.ps1"

Write-Host -ForegroundColor Green "Installing Java $ENV:JAVA_VERSION"

#$javazip= "https://download.java.net/java/early_access/jdk21/12/GPL/openjdk-21-ea+12_windows-x64_bin.zip"

## this is a bit fragile, and assumes the version will always be 
## NN-ea+VV.
$subver = ($ENV:JAVA_VERSION -split '\+')[1]
$javazip = "https://download.java.net/java/early_access/jdk21/$($subver)/GPL/openjdk-$($ENV:JAVA_VERSION)_windows-x64_bin.zip"

$out = 'java.zip'

Write-Host -ForegroundColor Green "Downloading $javazip to $out"

(New-Object System.Net.WebClient).DownloadFile($javazip, $out)
if ((Get-FileHash -Algorithm SHA256 $out).Hash -ne "$ENV:JAVA_SHA256") { Write-Host \"Wrong hashsum for ${out}: got '$((Get-FileHash -Algorithm SHA256 $out).Hash)', expected '$ENV:GO_SHA256'.\"; exit 1 }

mkdir c:\tmp\java

Write-Host -ForegroundColor Green "Extracting $out to c:\tmp\java"

# Start-Process "7z" -ArgumentList 'x -oc:\tmp\java java.zip' -Wait
Expand-Archive -Path $out -DestinationPath C:\tmp\java

Write-Host -ForegroundColor Green "Removing temporary file $out"

Remove-Item $out

Add-EnvironmentVariable -Variable JAVA_HOME -Value "c:\openjdk-21" -Global -Local

## move expanded file from tmp dir to final resting place
## note must be after env variable set above.

Move-Item -Path c:\tmp\java\* -Destination $Env:JAVA_HOME

Add-ToPath -NewPath "$($Env:JAVA_HOME)\bin" -Local -Global

Write-Host -ForegroundColor Green "Installed java $ENV:JAVA_VERSION"
Write-Host -ForegroundColor Green 'javac --version'; javac --version
Write-Host -ForegroundColor Green 'java --version'; java --version

## need to have more rigorous download at some point, but
#$jsignjarsrc = "https://s3.amazonaws.com/dd-agent-omnibus/jsign/jsign-4.2.jar"
$jsignjarsrc = "https://github.com/ebourg/jsign/releases/download/5.0/jsign-5.0.jar"
$jsignjardir = "c:\devtools\jsign"
$jsignout = "$($jsignjardir)\jsign-5.0.jar"
if (-Not (test-path $jsignjardir)) {
    mkdir $jsignjardir
}
(New-Object System.Net.WebClient).DownloadFile($jsignjarsrc, $jsignout)

Add-EnvironmentVariable -Variable JARSIGN_JAR -Value $jsignout -Global
