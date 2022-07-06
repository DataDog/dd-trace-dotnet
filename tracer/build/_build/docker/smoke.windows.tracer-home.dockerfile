ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework %PUBLISH_FRAMEWORK% -o /src/publish

FROM $RUNTIME_IMAGE AS publish
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /app

ARG CHANNEL_32_BIT
RUN if($env:CHANNEL_32_BIT){ \
    echo 'Installing x86 dotnet runtime ' + $env:CHANNEL_32_BIT; \
    curl 'https://dot.net/v1/dotnet-install.ps1' -o dotnet-install.ps1; \
    ./dotnet-install.ps1 -Architecture x86 -Runtime aspnetcore -Channel $env:CHANNEL_32_BIT -InstallDir c:\cli; \
    [Environment]::SetEnvironmentVariable('Path',  'c:\cli;' + $env:Path, [EnvironmentVariableTarget]::Machine); \
    rm ./dotnet-install.ps1; }

# Copy the tracer home file from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /install

RUN mkdir /logs; \
    mkdir /monitoring-home; \
    cd /install; \
    Expand-Archive 'c:\install\windows-tracer-home.zip' -DestinationPath 'c:\monitoring-home\';  \
    cd /app; \
    rm /install -r -fo


ARG RELATIVE_PROFILER_PATH

RUN [Environment]::SetEnvironmentVariable('CORECLR_PROFILER_PATH', 'c:\monitoring-home\' + $env:RELATIVE_PROFILER_PATH, [EnvironmentVariableTarget]::Machine);

# Set the additional env vars
ENV DD_PROFILING_ENABLED=1 \
    CORECLR_ENABLE_PROFILING=1 \
    CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
    DD_DOTNET_TRACER_HOME="c:\monitoring-home" \
    DD_TRACE_LOG_DIRECTORY="C:\logs" \
    ASPNETCORE_URLS=http://localhost:5000

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]