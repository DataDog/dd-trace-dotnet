# Differently from the Linux version, we assume that the SUT is already built
$ErrorActionPreference = "Stop"

# The suite lives in the dd-trace-dotnet checkout, four levels below its root.
Set-Location -Path "$PSScriptRoot/../../../../tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController"

# Needed for runtime metrics to work - https://docs.datadoghq.com/tracing/metrics/runtime_metrics/?tab=net
net localgroup "Performance Monitor Users" "IIS APPPOOL\DefaultAppPool" /add

$env:ASPNETCORE_URLS = "http://localhost:8080"
$env:DOTNET_GCHeapHardLimit = 0x10000000  # 256 MB
$env:DOTNET_GCHeapCount = 0x2  # 2 heaps
C:\dotnet\dotnet.exe "./bin/Debug/net9.0/Samples.AspNetCoreSimpleController.dll"
