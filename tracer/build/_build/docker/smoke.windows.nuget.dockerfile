ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Without this, the container doesn't have permission to add the new package 
USER ContainerAdministrator

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet restore "AspNetCoreSmokeTest.csproj" \
    && dotnet nuget add source "c:\src\artifacts" \
    && dotnet add package "Datadog.Monitoring.Distribution" --prerelease \
    && dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework %PUBLISH_FRAMEWORK% -o "c:\src\publish"

FROM $RUNTIME_IMAGE AS publish-msi
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /app

ARG CHANNEL_32_BIT
RUN if($env:CHANNEL_32_BIT){ \
    echo 'Installing x86 dotnet runtime ' + $env:CHANNEL_32_BIT; \
    curl 'https://dot.net/v1/dotnet-install.ps1' -o dotnet-install.ps1; \
    ./dotnet-install.ps1 -Architecture x86 -Runtime aspnetcore -Channel $env:CHANNEL_32_BIT -InstallDir c:\cli; \
    [Environment]::SetEnvironmentVariable('Path',  'c:\cli;' + $env:Path, [EnvironmentVariableTarget]::Machine); \
    rm ./dotnet-install.ps1; }

RUN mkdir /logs

ARG RELATIVE_PROFILER_PATH

RUN [Environment]::SetEnvironmentVariable('CORECLR_PROFILER_PATH',  $env:RELATIVE_PROFILER_PATH, [EnvironmentVariableTarget]::Machine);
# Set the required env vars
ENV CORECLR_ENABLE_PROFILING=1 \
    CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
    DD_DOTNET_TRACER_HOME="c:\app\datadog" \
    DD_TRACE_LOG_DIRECTORY="C:\logs" \
    DD_PROFILING_ENABLED=1 \
    ASPNETCORE_URLS=http://localhost:5000

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]