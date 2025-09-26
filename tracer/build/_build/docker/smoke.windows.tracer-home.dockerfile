ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK in the windows server core image
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-windowsservercore-ltsc2022 as builder
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework $env:PUBLISH_FRAMEWORK -o /src/publish

ARG CHANNEL_32_BIT
RUN if($env:CHANNEL_32_BIT){ \
    echo 'Installing x86 dotnet runtime ' + $env:CHANNEL_32_BIT; \
    curl 'https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1' -o dotnet-install.ps1; \
    ./dotnet-install.ps1 -Architecture x86 -Runtime aspnetcore -Channel $env:CHANNEL_32_BIT -InstallDir c:\cli; \
    rm ./dotnet-install.ps1; }

RUN mkdir /logs; \
    mkdir /monitoring-home; \
    cd /src/artifacts; \
    Expand-Archive 'c:\src/artifacts\windows-tracer-home.zip' -DestinationPath 'c:\monitoring-home\';  \
    cd /app; \
    rm /src/artifacts -r -fo


# ---- Runtime stage -----------------------------------------------------------
FROM $RUNTIME_IMAGE AS publish
# We have to use cmd instead of powershell, because nanoserver doesn't have it
SHELL ["cmd", "/S", "/C"]

# Make sure the path is set, in cases we have installed the 32 bit runtime
ENV PATH=C:\cli;%PATH%

ARG RELATIVE_PROFILER_PATH
ENV CORECLR_PROFILER_PATH="C:\monitoring-home\${RELATIVE_PROFILER_PATH}"

# Runtime env
ENV DD_PROFILING_ENABLED=1
ENV DD_TRACE_DEBUG=1
ENV DD_APPSEC_ENABLED=1
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV DD_DOTNET_TRACER_HOME="C:\monitoring-home"
ENV DD_TRACE_LOG_DIRECTORY="C:\logs"
ENV DD_REMOTE_CONFIGURATION_ENABLED=0
ENV ASPNETCORE_URLS=http://localhost:5000

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Copy across the 32 bit runtime (if it's there) and the install dir
COPY --from=builder C:\monitoring-home C:\monitoring-home
COPY --from=builder C:\cli C:\cli

WORKDIR /app

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]
