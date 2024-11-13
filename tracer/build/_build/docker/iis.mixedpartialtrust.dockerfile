FROM mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2022
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Copy IIS website
# Reusing the MultipleAppsInDomain, but replacing the custom config builder
ADD tracer/test/test-applications/aspnet/Samples.AspNet.MultipleAppsInDomain/bin/Release/publish MixedPartialTrust
# Replace the custom config builder with the default one
RUN (Get-Content c:\MixedPartialTrust\Web.config).Replace('<appSettings configBuilders="CustomBuilder">', '<appSettings>') | Set-Content c:\MixedPartialTrust\Web.config

# Set up multiple apps in single domain with IIS websites
ARG ENABLE_32_BIT
ENV ENABLE_32_BIT=${ENABLE_32_BIT:-false}

RUN c:\Windows\System32\inetsrv\appcmd add apppool /name:mutliAppPool /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated" /enable32bitapponwin64:$env:ENABLE_32_BIT

# The SetEnvironmentVariable() calls shouldn't _need_ to be here - we should be able to do it just
# using docker-compose, but I can't get that to work for some inexplicable reason, no matter
# what I do, so here we are.
RUN Remove-WebSite -Name 'Default Web Site'; \
    Write-Host "Created app pool with 32 bit reg key: $env:ENABLE_32_BIT"; \
    Write-Host "Creating multi app domain sites"; \
    [System.Environment]::SetEnvironmentVariable('DD_TRACE_AGENT_URL', 'http://test-agent.windows:8126', [System.EnvironmentVariableTarget]::Machine); \
    [System.Environment]::SetEnvironmentVariable('DD_TRACE_AGENT_URL', 'http://test-agent.windows:8126', [System.EnvironmentVariableTarget]::Process); \
    [System.Environment]::SetEnvironmentVariable('DD_TRACE_AGENT_URL', 'http://test-agent.windows:8126', [System.EnvironmentVariableTarget]::User); \
    New-Website -Name 'MultiAppPoolWithFullTrust' -ApplicationPool mutliAppPool -Port 8083 -PhysicalPath 'c:\MixedPartialTrust'; \
    New-Website -Name 'MultiAppPoolWithPartialTrust' -ApplicationPool mutliAppPool -Port 8084 -PhysicalPath 'c:\MixedPartialTrust';

# Enabling partial trust for application 2
RUN c:\Windows\System32\inetsrv\appcmd set Config "MultiAppPoolWithPartialTrust" /section:trust /level:High

# Install the .NET Tracer MSI
ARG DOTNET_TRACER_MSI
ADD $DOTNET_TRACER_MSI ./datadog-apm.msi
RUN Start-Process -Wait msiexec -ArgumentList '/qn /i datadog-apm.msi'

# Restart IIS
RUN net stop /y was; \
    net start w3svc

EXPOSE 80