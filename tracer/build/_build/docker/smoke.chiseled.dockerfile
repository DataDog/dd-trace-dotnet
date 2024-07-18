ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

# Creating an empty we can copy in the publish stage to create the logs folder
RUN mkdir -p /empty

###########################################################
FROM $RUNTIME_IMAGE AS publish

WORKDIR /src

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

ENV ASPNETCORE_URLS=http://localhost:5000

# Copy the empty folder to create the logs directory
COPY --chown=$APP_UID --from=builder /empty/ /var/log/datadog/dotnet/

WORKDIR /app

# Copy the app across
COPY --from=builder /src/publish /app/.

###########################################################
FROM publish as installer-base

# Add and extract the installer files to the expected location
# from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
ADD ./test/test-applications/regression/AspNetCoreSmokeTest/artifacts/datadog-dotnet-apm-*.tar.gz /opt/datadog

###########################################################
# The final image, with "manual" configuration
FROM installer-base as installer-final

# Set the required env vars
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/opt/datadog
ENV LD_PRELOAD=/opt/datadog/continuousprofiler/Datadog.Linux.ApiWrapper.x64.so
ENV DD_PROFILING_ENABLED=1
ENV DD_APPSEC_ENABLED=1
ENV DD_TRACE_DEBUG=1
ENV COMPlus_DbgEnableMiniDump=1
ENV COMPlus_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/dumps/coredump.%t.%p

## SSI variables
ENV DD_INJECTION_ENABLED=tracer
ENV DD_INJECT_FORCE=1
ENV DD_TELEMETRY_FORWARDER_PATH=/bin/true

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]

###########################################################
# The final image, with "dd-dotnet" configuration
# Note that we _can't_ use dd-dotnet-sh in this case
# Because there _is_ no shell in the chiseled containers
# Also, we can't use env vars/args in the entrypoint, hence the need for separate targets
FROM installer-base as dd-dotnet-final-linux-x64
ENTRYPOINT ["/opt/datadog/linux-x64/dd-dotnet", "run", "--set-env", "DD_PROFILING_ENABLED=1","--set-env", "DD_APPSEC_ENABLED=1","--set-env", "DD_TRACE_DEBUG=1", "--", "dotnet", "/app/AspNetCoreSmokeTest.dll"]

###########################################################
FROM installer-base as dd-dotnet-final-linux-arm64
ENTRYPOINT ["/opt/datadog/linux-arm64/dd-dotnet", "run", "--set-env", "DD_PROFILING_ENABLED=1","--set-env", "DD_APPSEC_ENABLED=1","--set-env", "DD_TRACE_DEBUG=1", "--", "dotnet", "/app/AspNetCoreSmokeTest.dll"]
