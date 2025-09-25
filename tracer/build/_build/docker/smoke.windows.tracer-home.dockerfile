ARG SDK_TAG
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$SDK_TAG as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework %PUBLISH_FRAMEWORK% -o /src/publish

# ---- Runtime stage -----------------------------------------------------------
FROM $RUNTIME_IMAGE AS publish
# We have to use cmd instead of powershell, because nanoserver doesn't have 
SHELL ["cmd", "/S", "/C"]

WORKDIR /app

# Make sure the path is set, in cases we have to install the 32 bit runtime
ENV PATH=C:\cli;%PATH%

# Only install x86 ASP.NET Core runtime on Server Core (PowerShell available).
# On NanoServer, PowerShell isn't present, so this becomes a no-op.
ARG CHANNEL_32_BIT
RUN IF DEFINED CHANNEL_32_BIT ( \
    IF EXIST C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe ( \
        echo Installing x86 ASP.NET Core runtime channel %CHANNEL_32_BIT%... && \
        curl.exe -L "https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1" -o dotnet-install.ps1 && \
        C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\dotnet-install.ps1" -Architecture x86 -Runtime aspnetcore -Channel %CHANNEL_32_BIT% -InstallDir C:\cli && \
        del /q dotnet-install.ps1 \
    ) ELSE ( \
        echo NanoServer detected (no PowerShell). Skipping x86 runtime install. \
    ) \
) ELSE ( \
    echo CHANNEL_32_BIT not set. Skipping x86 runtime install. \
)

# Copy the tracer home file from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /install

# Create dirs, extract zip with tar (works on NanoServer), clean up
RUN mkdir C:\logs && \
    mkdir C:\monitoring-home && \
    tar -xf C:\install\windows-tracer-home.zip -C C:\monitoring-home && \
    rmdir /S /Q C:\install

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

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]
