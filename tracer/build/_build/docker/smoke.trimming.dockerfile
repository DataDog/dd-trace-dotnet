ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

# Enable trimming for the app, and add the necessary NuGet packages that include the trimmer files
ENV PublishTrimmed=true
ARG TOOL_VERSION
ARG PACKAGE_NAME
ARG PUBLISH_FRAMEWORK
ARG RUNTIME_IDENTIFIER
RUN dotnet restore "AspNetCoreSmokeTest.csproj" \
    && dotnet nuget add source /src/artifacts \
    && echo "Installing $PACKAGE_NAME version $TOOL_VERSION for $RUNTIME_IDENTIFIER" \
    && dotnet add package $PACKAGE_NAME --version $TOOL_VERSION \
    && dotnet publish "AspNetCoreSmokeTest.csproj" -r $RUNTIME_IDENTIFIER -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

FROM $RUNTIME_IMAGE AS publish

WORKDIR /app

# Copy the installer files from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /app/install

ARG INSTALL_CMD
RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog \
    && cd /app/install \
    && $INSTALL_CMD \
    && rm -rf /app/install

# Set the required env vars
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/opt/datadog
ENV LD_PRELOAD=/opt/datadog/continuousprofiler/Datadog.Linux.ApiWrapper.x64.so
ENV DD_PROFILING_ENABLED=1
ENV DD_APPSEC_ENABLED=1
ENV DD_TRACE_DEBUG=1
ENV DD_REMOTE_CONFIGURATION_ENABLED=0

## SSI variables
ENV DD_INJECTION_ENABLED=tracer
ENV DD_INJECT_FORCE=1
ENV DD_TELEMETRY_FORWARDER_PATH=/bin/true

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

ENV ASPNETCORE_URLS=http://localhost:5000

# Capture dumps
ENV COMPlus_DbgEnableMiniDump=0
ENV COMPlus_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/dumps/coredump.%t.%p
ENV DOTNET_EnableCrashReport=0
ENV DD_CRASHTRACKING_ENABLED=0

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["./AspNetCoreSmokeTest"]
