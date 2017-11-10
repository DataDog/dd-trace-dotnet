# This script downloads the 5.19.0 version of the trace-agent and runs it in the background for the integration tests
Invoke-WebRequest https://github.com/DataDog/datadog-trace-agent/releases/download/5.19.0/trace-agent-windows-5.19.0.exe -OutFile trace-agent-windows.exe
Start-Process trace-agent-windows.exe "--ddconfig $PSScriptRoot\config.ini"
