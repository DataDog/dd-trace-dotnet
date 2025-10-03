# .NET Framework Installation

**Supported versions:** .NET Framework >= 4.6.1

## Installation Methods

### 1. Machine-wide installation (Windows)

- MSI installer from GitHub releases
- x64 MSI for 64-bit Windows (instruments both 64-bit and 32-bit applications)
- x86 MSI for 32-bit Windows only
- Starting with v3.0.0, only x64 installer is provided (32-bit OS not supported)
- Script: `Start-Process -Wait msiexec -ArgumentList '/qn /i datadog-apm.msi'`

### 2. Per-application installation (NuGet)

- Package: `Datadog.Trace.Bundle`
- Does not instrument IIS applications; use machine-wide installation for IIS
- Follow instructions in package README (also available in `dd-trace-dotnet` repository)

## Enabling the Tracer

### Windows (IIS)

- MSI installer sets all required environment variables
- Restart IIS: `net stop /y was && net start w3svc` (do not use IIS Manager or `iisreset.exe`)
- To set additional environment variables inherited by all IIS sites:
  1. Open Registry Editor: `HKLM\System\CurrentControlSet\Services\WAS`
  2. Edit multi-string value `Environment` (add one variable per line)
  3. Example: `DD_LOGS_INJECTION=true` and `DD_RUNTIME_METRICS_ENABLED=true`
  4. Restart IIS with commands above

### Windows Services

- Set environment variables in Registry Editor at `HKLM\System\CurrentControlSet\Services\<SERVICE NAME>`
- Create multi-string value `Environment` with value `COR_ENABLE_PROFILING=1`
- PowerShell: `Set-ItemProperty HKLM:SYSTEM\CurrentControlSet\Services\<SERVICE NAME> -Name Environment -Value 'COR_ENABLE_PROFILING=1'`
- Restart the service

### Console Applications

Set environment variables in batch file before starting application:

```bat
rem Set required environment variables
SET COR_ENABLE_PROFILING=1

rem (Optionally) Set additional Datadog environment variables
SET DD_LOGS_INJECTION=true
SET DD_RUNTIME_METRICS_ENABLED=true

rem Start application
dotnet.exe example.dll
```

## Custom Instrumentation (.NET Framework)

- Starting with v3.0.0, custom instrumentation requires automatic instrumentation
- Keep automatic and custom instrumentation versions in sync; avoid mixing major versions
- Steps:
  1. Instrument application using automatic instrumentation
  2. Add `Datadog.Trace` NuGet package to application
  3. Access global tracer via `Datadog.Trace.Tracer.Instance` to create new spans

## Important Notes

- .NET CLR Profiling API allows only one subscriber; run only one APM solution per environment
- Machine-wide installation affects all .NET applications on the host
- Do not set profiling environment variables globally; limit to specific applications to avoid instrumenting all .NET processes
