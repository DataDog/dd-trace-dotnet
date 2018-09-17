[Net.ServicePointManager]::SecurityProtocol = "tls12, tls11, tls"

$tmp = [System.IO.Path]::GetTempPath()
$installerUrl = "https://github.com/MicrosoftArchive/redis/releases/download/win-3.2.100/Redis-x64-3.2.100.msi"
$installerPath = Join-Path $tmp "install-redis.msi"
$installerLog = Join-Path $tmp 'install-redis.log'

if (Test-Path "C:\Program Files\Redis\redis-server.exe") {
    Write-Output "redis is already installed"
    Exit 0;
}

if (-not (Test-Path $installerPath)) {
    Write-Output "downloading redis to $installerPath"
    (New-Object Net.WebClient).DownloadFile($installerUrl, $installerPath)
}

Write-Output "installing redis"
Start-Process "msiexec.exe" -ArgumentList "/i",$installerPath,"/qn","/norestart","/L",$installerLog -Wait -NoNewWindow
Get-Content $installerLog
