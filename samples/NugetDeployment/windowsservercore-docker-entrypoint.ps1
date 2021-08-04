# Workaround from # Workaround from https://github.com/docker/for-win/issues/1976#issuecomment-585423014
# DNS Entries
Write-Host("Configuring DNS entries...")
.\windowsservercore-docker-fix-hosts.ps1
.\HttpListenerExample.exe