
REM This is a required entry point for PowerShell scripts in Azure Service Fabric
powershell.exe -ExecutionPolicy Bypass -Command ".\DatadogInstall.ps1" > %SvcFabDir%\datadog-powershell-log.txt 2> %SvcFabDir%\datadog-powershell-error.txt
