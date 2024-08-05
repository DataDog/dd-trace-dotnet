### Stage: build
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln ./
COPY src/*.csproj ./src/
COPY src/*.config ./src/
RUN nuget restore

# copy everything else and build app
COPY src/. ./src/
WORKDIR /app/src
RUN msbuild /p:Configuration=Release

### Stage: runtime
FROM mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2019 AS runtime
WORKDIR /inetpub/wwwroot
COPY --from=build /app/src/. ./
COPY install-dd-trace-dotnet.ps1 startup.ps1 ./

# Install the Datadog SDK for .NET using the msi installer
RUN .\install-dd-trace-dotnet.ps1

# Note: Normally an IIS reset is also required but 'C:\ServiceMonitor.exe w3svc'
# will restart IIS and update the environment variables received by the child w3wp processes
# Replace the default ENTRYPOINT "C:\ServiceMonitor.exe w3svc"
ENTRYPOINT .\startup.ps1
