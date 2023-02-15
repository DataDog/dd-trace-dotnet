### Stage: build
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .
COPY src/*.csproj ./src/
COPY src/*.config ./src/
RUN nuget restore

# copy everything else and build app
COPY src/. ./src/
WORKDIR /app/src
RUN msbuild /p:Configuration=Release

### Stage: runtime
FROM mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2019 AS runtime
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /inetpub/wwwroot
COPY --from=build /app/src/. ./
COPY startup.ps1 .

ARG TRACER_VERSION=2.23.0
ENV DD_TRACER_VERSION=$TRACER_VERSION

# Install the .NET Tracer MSI
# Note: Normally an IIS reset is also required but 'C:\ServiceMonitor.exe w3svc'
# will restart IIS and update the environment variables received by the child w3wp processes
RUN Write-Host "Downloading Datadog .NET Tracer v$env:DD_TRACER_VERSION" ;\
    (New-Object System.Net.WebClient).DownloadFile('https://github.com/DataDog/dd-trace-dotnet/releases/download/v' + $env:DD_TRACER_VERSION + '/datadog-dotnet-apm-' + $env:DD_TRACER_VERSION + '-x64.msi', 'datadog-apm.msi') ;\
    Write-Host 'Installing Datadog .NET Tracer' ;\
    Start-Process -Wait msiexec -ArgumentList '/i datadog-apm.msi /quiet /qn /norestart /log datadog-apm-msi-installer.log' ; \
    Write-Host 'Datadog .NET Tracer installed, removing installer file' ; \
    Remove-Item 'datadog-apm.msi' ;

ENTRYPOINT ["powershell.exe", ".\\startup.ps1"]
