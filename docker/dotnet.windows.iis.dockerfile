FROM mcr.microsoft.com/windows/servercore/iis:windowsservercore-ltsc2019 as final
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Install .NET Framework, and ASP.NET features
RUN Add-WindowsFeature NET-Framework-45-ASPNET; \
    Add-WindowsFeature Web-Asp-Net45

# Copy IIS websites
ADD samples-iis samples-iis

# Set up IIS websites
RUN Remove-WebSite -Name 'Default Web Site'; \
    New-Website -Name 'loaderoptimization-startup' -Port 80 -PhysicalPath 'c:\samples-iis\Samples.AspNet472.LoaderOptimizationRegKey\bin\Release\Publish'

# Set LoaderOptimization flag to recreate crash condition
RUN New-ItemProperty -Path "HKLM:\Software\Microsoft\.NETFramework" -Name "LoaderOptimization" -Value 1

# Install datadog-apm-x64.msi
ARG DOTNET_TRACER_MSI
ADD $DOTNET_TRACER_MSI ./datadog-apm-x64.msi
RUN Start-Process -Wait msiexec -ArgumentList '/qn /i datadog-apm-x64.msi'

# Restart IIS
RUN net stop /y was; \
    net start w3svc

EXPOSE 80