FROM mcr.microsoft.com/windows/servercore/iis:windowsservercore-ltsc2019
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Install .NET Framework, and ASP.NET features
RUN Add-WindowsFeature NET-Framework-45-ASPNET; \
    Add-WindowsFeature Web-Asp-Net45

# Copy IIS websites
ADD samples-iis/Samples.AspNet472.LoaderOptimizationRegKey/bin/Release/Publish LoaderOptimizationRegKey

# Set up IIS websites
ARG ENABLE_32_BIT
RUN Remove-WebSite -Name 'Default Web Site'; \
    New-Website -Name 'LoaderOptimizationRegKey' -Port 80 -PhysicalPath 'c:\LoaderOptimizationRegKey'
RUN c:\Windows\System32\inetsrv\appcmd set apppool /apppool.name:DefaultAppPool /enable32bitapponwin64:$env:ENABLE_32_BIT

# Set LoaderOptimization flag to recreate crash condition (both 64-bit and 32-bit)
RUN New-ItemProperty -Path "HKLM:\Software\Microsoft\.NETFramework" -Name "LoaderOptimization" -Value 1
RUN New-ItemProperty -Path "HKLM:\Software\WOW6432Node\Microsoft\.NETFramework" -Name "LoaderOptimization" -Value 1

# Install the .NET Tracer MSI
ARG DOTNET_TRACER_MSI
ADD $DOTNET_TRACER_MSI ./datadog-apm.msi
RUN Start-Process -Wait msiexec -ArgumentList '/qn /i datadog-apm.msi'

# Restart IIS
RUN net stop /y was; \
    net start w3svc

EXPOSE 80