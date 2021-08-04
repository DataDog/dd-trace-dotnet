# DNS Entries
Write-Host("Configuring DNS entries...")
.\windowsservercore-docker-fix-hosts.ps1
.\HttpListenerExample.exe