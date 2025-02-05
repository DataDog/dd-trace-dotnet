ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-windowsservercore-ltsc2022 as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework %PUBLISH_FRAMEWORK% -o /src/publish

FROM $RUNTIME_IMAGE AS publish
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /inetpub/wwwroot

ARG CHANNEL
ARG TARGET_PLATFORM
COPY ./build/_build/bootstrap/dotnet-install.ps1 .
RUN echo 'Installing ' + $env:TARGET_PLATFORM +  ' dotnet runtime ' + $env:CHANNEL; \
    ./dotnet-install.ps1 -Architecture $env:TARGET_PLATFORM -Runtime aspnetcore -Channel $env:CHANNEL -InstallDir c:\cli; \
    [Environment]::SetEnvironmentVariable('Path',  'c:\cli;' + $env:Path, [EnvironmentVariableTarget]::Machine); \
    rm ./dotnet-install.ps1;

# Copy the tracer home file from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /install

RUN mkdir /logs; \
    mkdir /monitoring-home; \
    cd /install; \
    Expand-Archive 'c:\install\windows-tracer-home.zip' -DestinationPath 'c:\monitoring-home\';  \
    c:\install\installer\Datadog.FleetInstaller.exe install --home-path c:\monitoring-home; \
    cd /inetpub/wwwroot;

# Set the additional env vars
ENV DD_PROFILING_ENABLED=1 \
    DD_TRACE_DEBUG=1 \
    DD_APPSEC_ENABLED=1 \
    DD_DOTNET_TRACER_HOME="c:\monitoring-home" \
    DD_TRACE_LOG_DIRECTORY="C:\logs"

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Copy the app across
COPY --from=builder /src/publish /inetpub/wwwroot/.
