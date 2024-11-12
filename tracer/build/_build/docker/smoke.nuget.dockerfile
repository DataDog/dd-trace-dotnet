ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG TOOL_VERSION
ARG PUBLISH_FRAMEWORK
RUN dotnet restore "AspNetCoreSmokeTest.csproj" \
    && dotnet nuget add source /src/artifacts \
    && dotnet add package "Datadog.Trace.Bundle" --version $TOOL_VERSION \
    && dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

FROM $RUNTIME_IMAGE AS publish

WORKDIR /app

RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog

# Copy the app across
COPY --from=builder /src/publish /app/.

ARG RELATIVE_PROFILER_PATH
ARG RELATIVE_APIWRAPPER_PATH

# Set the required env vars
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/app/${RELATIVE_PROFILER_PATH}
ENV LD_PRELOAD=/app/${RELATIVE_APIWRAPPER_PATH}
ENV DD_DOTNET_TRACER_HOME=/app/datadog
ENV DD_PROFILING_ENABLED=1
ENV DD_APPSEC_ENABLED=1
ENV DD_TRACE_DEBUG=1

## SSI variables
ENV DD_INJECTION_ENABLED=tracer
ENV DD_INJECT_FORCE=1
ENV DD_TELEMETRY_FORWARDER_PATH=/bin/true

ENV ASPNETCORE_URLS=http://localhost:5000

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Capture dumps
ENV COMPlus_DbgEnableMiniDump=1
ENV COMPlus_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/dumps/coredump.%t.%p


ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]
