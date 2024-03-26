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

# Install the .NET Tracer MSI
# Note: Normally an IIS reset is also required but 'C:\ServiceMonitor.exe w3svc'
# will restart IIS and update the environment variables received by the child w3wp processes
RUN Write-Host 'Determining latest release' ;\
    $releases = 'https://api.github.com/repos/DataDog/dd-trace-dotnet/releases' ;\
    $tag = (Invoke-WebRequest $releases -UseBasicParsing | ConvertFrom-Json)[0].tag_name ;\
    $version = $tag.Split('v')[1] ;\
    Write-Host "Downloading Datadog .NET Tracer $tag" ;\
    (New-Object System.Net.WebClient).DownloadFile('https://github.com/DataDog/dd-trace-dotnet/releases/download/' + $tag + '/datadog-dotnet-apm-' + $version + '-x64.msi', 'datadog-apm.msi') ;\
    Write-Host 'Installing Datadog .NET Tracer' ;\
    Start-Process -Wait msiexec -ArgumentList '/i datadog-apm.msi /quiet /qn /norestart /log datadog-apm-msi-installer.log' ; \
    Write-Host 'Datadog .NET Tracer installed, removing installer file' ;\
    Remove-Item 'datadog-apm.msi' ;

ENTRYPOINT ["powershell.exe", ".\\startup.ps1"]
