#!/bin/sh
echo "=== Datadog tracer check ==="
echo "CORECLR_ENABLE_PROFILING  = $CORECLR_ENABLE_PROFILING"
echo "CORECLR_PROFILER          = $CORECLR_PROFILER"
echo "CORECLR_PROFILER_PATH     = $CORECLR_PROFILER_PATH"
echo "DD_DOTNET_TRACER_HOME     = $DD_DOTNET_TRACER_HOME"
echo "DD_EXCEPTION_REPLAY_ENABLED = $DD_EXCEPTION_REPLAY_ENABLED"
echo "DD_INJECTION_ENABLED      = $DD_INJECTION_ENABLED"
echo "DD_TRACE_AGENT_URL        = $DD_TRACE_AGENT_URL"
echo ""

# Verify tracer files
echo "=== Tracer files ==="
ls -la /opt/datadog/loader.conf
ls -la /opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ls -la /opt/datadog/linux-x64/Datadog.Tracer.Native.so
echo ""

echo "=== Starting app ==="
exec dotnet /app/ReproApp.dll
