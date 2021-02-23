  
# Write-Host "[entrypoint] Installing tracer'"
# .\install-tracer.ps1
# 
# Write-Host "[entrypoint] Restarting IIS'"
# .\restart-iis.ps1

Write-Host "[entrypoint] Starting original Windows IIS entrypoint"
C:\ServiceMonitor.exe w3svc
