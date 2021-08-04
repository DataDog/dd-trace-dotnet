# DNS Entries
Write-Host("Configuring DNS entries...")
.\windows-docker-fix-hosts.ps1
.\HttpListenerExample.exe