# .NET Core / .NET 5+ Installation

**Supported versions:** .NET Core 3.1, .NET 5, .NET 6, .NET 7, .NET 8, .NET 9

## Installation Methods

### 1. Machine-wide installation (Windows/Linux)

#### Windows
- MSI installer from GitHub releases (x64/x86)
- Script: `Start-Process -Wait msiexec -ArgumentList '/qn /i datadog-apm.msi'`

#### Linux
- Package installation (DEB/RPM/tar.gz)
- Debian/Ubuntu: `sudo dpkg -i ./datadog-dotnet-apm_<VERSION>_amd64.deb && /opt/datadog/createLogPath.sh`
- CentOS/Fedora: `sudo rpm -Uvh datadog-dotnet-apm-<VERSION>-1.x86_64.rpm && /opt/datadog/createLogPath.sh`
- Alpine/musl: `sudo tar -C /opt/datadog -xzf datadog-dotnet-apm-<VERSION>-musl.tar.gz && sh /opt/datadog/createLogPath.sh`
- Other: `sudo tar -C /opt/datadog -xzf datadog-dotnet-apm-<VERSION>.tar.gz && /opt/datadog/createLogPath.sh`

#### Chiseled/distroless containers
Use `ADD` for tracer and `COPY --chown=$APP_UID` for log directory

### 2. Per-application installation (NuGet)

- Package: `Datadog.Trace.Bundle`
- Does not instrument IIS applications; use machine-wide installation for IIS

## Enabling the Tracer

### Windows (IIS)

- MSI installer sets all required environment variables
- Set .NET CLR version for application pool to "No Managed Code" (recommended by Microsoft)
- Restart IIS: `net stop /y was && net start w3svc` (do not use IIS Manager or `iisreset.exe`)

### Windows (Non-IIS) / Linux

- Set `CORECLR_ENABLE_PROFILING=1`
- Restart application/service

### Trimmed Applications

- Reference `Datadog.Trace.Trimming` NuGet package for trimmed app support

## Important Notes

- .NET CLR Profiling API allows only one subscriber; run only one APM solution per environment
- Machine-wide installation affects all .NET applications on the host
- Do not set profiling environment variables globally; limit to specific applications to avoid instrumenting all .NET processes
- For serverless environments (AWS Lambda, Azure Functions), see corresponding serverless documentation
