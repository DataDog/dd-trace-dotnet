FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ReproApp.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --nologo

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

# Download the pre-built linux-x64 v3.41.0 tracer from GitHub releases.
# This is the BUGGY version (before PR #8428 "Fix EH clause sort to handle try-in-handler nesting").
# The x64 tarball installs into /opt/datadog and includes:
#   linux-x64/Datadog.Tracer.Native.so   ← main native tracer
#   Datadog.Trace.ClrProfiler.Native.so  ← loader shim
#   loader.conf                           ← maps RID to .so paths
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    mkdir -p /opt/datadog && \
    curl -fsSL "https://github.com/DataDog/dd-trace-dotnet/releases/download/v3.41.0/datadog-dotnet-apm-3.41.0.tar.gz" \
         -o /tmp/ddtrace.tar.gz && \
    tar -C /opt/datadog -xzf /tmp/ddtrace.tar.gz && \
    rm /tmp/ddtrace.tar.gz && \
    apt-get purge -y curl && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*

# Fix permissions and verify all deps are resolvable at build time
RUN find /opt/datadog -name "*.so" -exec chmod 755 {} \; && \
    find /opt/datadog -name "dd-dotnet" -exec chmod 755 {} \; && \
    find /opt/datadog -name "*.sh" -exec chmod 755 {} \; && \
    # Create the log directory the tracer expects
    mkdir -p /var/log/datadog/dotnet && chmod a+rwx /var/log/datadog/dotnet && \
    # Verify deps are available (build will fail loudly if not)
    echo "=== Loader deps ===" && ldd /opt/datadog/Datadog.Trace.ClrProfiler.Native.so && \
    echo "=== Tracer deps ===" && ldd /opt/datadog/linux-x64/Datadog.Tracer.Native.so

# Wire up the CLR profiler — loader.conf is at DD_DOTNET_TRACER_HOME/loader.conf
# and resolves ./linux-x64/Datadog.Tracer.Native.so relative to that dir
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/opt/datadog

# Fix LD_PRELOAD for profiler to work properly
ENV LD_PRELOAD=/opt/datadog/linux-x64/Datadog.Linux.ApiWrapper.x64.so

# SSI injection marker — required by the native loader to activate
ENV DD_INJECTION_ENABLED=tracer

# Feature flags — match reported incident (ER + AppSec + IAST)
ENV DD_EXCEPTION_REPLAY_ENABLED=true
ENV DD_APPSEC_ENABLED=true
ENV DD_IAST_ENABLED=true
ENV DD_TRACE_DEBUG=true
ENV DD_TRACE_LOG_DIRECTORY=/var/log/datadog/dotnet

# Dump rewritten IL (optional debugging)
ENV DD_DUMP_ILREWRITE_ENABLED=true

# Uncomment to force debugger probe instrumentation on ALL methods at JIT time.
# This bypasses the ER pipeline entirely and instruments MoveNext at first JIT,
# producing the SAME EH clause layout as ER's ReJIT, but without needing to
# trigger the ER observation pipeline first. Useful as a secondary crash test.
#ENV DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true

# Point agent URL to a mock agent (see docker-compose.yml)
ENV DD_TRACE_AGENT_URL=http://agent:8126
ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000

# Entrypoint script — dumps env + confirms tracer files are present before starting
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
