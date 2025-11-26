#!/bin/bash
set -e

echo "=== Testing Hybrid Unwinding with .NET 10 ==="

ARCH=$(uname -m)
case "$ARCH" in
    x86_64)
        BUILD_ARCH="x64"
        TARGET_RID="linux-musl-x64"
        API_WRAPPER="Datadog.Linux.ApiWrapper.x64.so"
        ;;
    aarch64)
        BUILD_ARCH="arm64"
        TARGET_RID="linux-musl-arm64"
        API_WRAPPER="Datadog.Linux.ApiWrapper.arm64.so"
        ;;
    *)
        echo "ERROR: Unsupported architecture $ARCH"
        exit 1
        ;;
esac

echo "Detected architecture: ${ARCH} (build=${BUILD_ARCH}, rid=${TARGET_RID})"

# Clean up any previous test results and use mounted directories
rm -rf /project/test_logs /project/test_profiles
mkdir -p /project/test_logs /project/test_profiles

# Set up profiler environment variables (matching integration tests)
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
export DD_DOTNET_TRACER_HOME="/project/shared/bin/monitoring-home/"
export CORECLR_PROFILER_PATH="/project/shared/bin/monitoring-home/${TARGET_RID}/Datadog.Trace.ClrProfiler.Native.so"
export DD_PROFILING_ENABLED=1
export DD_PROFILING_CPU_ENABLED=1
export DD_PROFILING_WALLTIME_ENABLED=1
export DD_INTERNAL_PROFILING_OUTPUT_DIR="/project/test_profiles"
export DD_TRACE_LOG_DIRECTORY="/project/test_logs"
export DD_INTERNAL_USE_HYBRID_UNWINDING=1 # <-- Thing to change to test hybrid unwinding
export DD_TRACE_DEBUG=0 # <-- Thing to change to test if logging is crashing things
export DD_TRACE_ENABLED=0
# export DD_PROFILING_MANAGED_ACTIVATION_ENABLED=0
export DD_SERVICE="hybrid-unwinding-test"
# Default should be OK
# export DD_TRACE_AGENT_HOSTNAME="127.0.0.1"
# export DD_TRACE_AGENT_PORT="8126"
export DD_PROFILING_UPLOAD_PERIOD="10"
WRAPPER_DIR="/project/shared/bin/monitoring-home/${TARGET_RID}"
WRAPPER_PATH="${WRAPPER_DIR}/${API_WRAPPER}"

if [ ! -f "$WRAPPER_PATH" ] && [ "$ARCH" = "aarch64" ]; then
    FALLBACK_WRAPPER="Datadog.Linux.ApiWrapper.x64.so"
    FALLBACK_PATH="${WRAPPER_DIR}/${FALLBACK_WRAPPER}"
    if [ -f "$FALLBACK_PATH" ]; then
        echo "ARM64-specific wrapper not found, using fallback ${FALLBACK_WRAPPER}"
        API_WRAPPER="$FALLBACK_WRAPPER"
        WRAPPER_PATH="$FALLBACK_PATH"
    fi
fi

export LD_PRELOAD="$WRAPPER_PATH"

PROFILER_NATIVE="/project/profiler/_build/DDProf-Deploy/${TARGET_RID}/Datadog.Profiler.Native.so"
TRACER_NATIVE="/project/shared/bin/monitoring-home/${TARGET_RID}/Datadog.Tracer.Native.so"

# Create a loader config file like the integration tests do
LOADER_CONFIG="/tmp/loader.conf"
cat > "$LOADER_CONFIG" << EOF
PROFILER;{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A};${TARGET_RID};${PROFILER_NATIVE}
TRACER;{50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5};${TARGET_RID};${TRACER_NATIVE}
EOF
export DD_NATIVELOADER_CONFIGFILE="$LOADER_CONFIG"

echo "Environment variables set:"
echo "  DD_INTERNAL_USE_HYBRID_UNWINDING=$DD_INTERNAL_USE_HYBRID_UNWINDING"
echo "  CORECLR_PROFILER_PATH=$CORECLR_PROFILER_PATH"

# Check if the profiler library exists
if [ ! -f "$CORECLR_PROFILER_PATH" ]; then
    echo "ERROR: Profiler library not found at $CORECLR_PROFILER_PATH"
    exit 1
fi

echo "Profiler library found: $(ls -la $CORECLR_PROFILER_PATH)"

if [ ! -f "$PROFILER_NATIVE" ]; then
    echo "ERROR: Profiler native binary not found at $PROFILER_NATIVE"
    exit 1
fi

if [ ! -f "$TRACER_NATIVE" ]; then
    echo "ERROR: Tracer native binary not found at $TRACER_NATIVE"
    exit 1
fi

if [ ! -f "$LD_PRELOAD" ]; then
    echo "ERROR: API wrapper not found at $LD_PRELOAD"
    exit 1
fi

echo "Using wrapper: $LD_PRELOAD"

# Navigate to the Computer01 .NET 10 directory
COMPUTER01_DIR="/project/profiler/_build/bin/Release-${BUILD_ARCH}/profiler/src/Demos/Samples.Computer01/net10.0"
if [ ! -d "$COMPUTER01_DIR" ]; then
    echo "ERROR: Computer01 .NET 10 directory not found at $COMPUTER01_DIR"
    exit 1
fi

cd "$COMPUTER01_DIR"
echo "Running from: $(pwd)"
echo "Available files: $(ls -la)"

# Run Computer01 with PI computation for 15 seconds
echo ""
echo "=== Starting Computer01 with PI computation (15 seconds) ==="
RUN_CMD=(dotnet Samples.Computer01.dll --timeout 15 --scenario PiComputation)
# Configure gdb to not stop on profiler signals
if [ "${ENABLED_DEBUGGER:-}" = "True" ]; then
    echo "ENABLED_DEBUGGER=True detected: launching application under gdb"
    
    # Create a gdb init script to handle profiler signals
    GDB_INIT="/tmp/gdb_init_$$"
    cat > "$GDB_INIT" << 'EOF'
handle SIGUSR1 nostop noprint pass
handle SIGPROF nostop noprint pass
handle SIGRTMIN nostop noprint pass
handle SIGRTMAX nostop noprint pass
EOF
    
    gdb -x "$GDB_INIT" --args "${RUN_CMD[@]}"
    rm -f "$GDB_INIT"
else
    "${RUN_CMD[@]}"
fi

# todo lldb conf
    # lldb \
    #   -o "plugin load /root/.dotnet/tools/.store/dotnet-sos/9.0.652701/dotnet-sos/9.0.652701/tools/net8.0/any/linux-musl-x64/libsosplugin.so" \
    #   -o "process launch" \
    #   -- "${RUN_CMD[@]}"

echo ""
echo "=== Application completed ==="

# Check results
echo ""
echo "=== Checking results ==="

echo "Generated profiles:"
ls -la /project/test_profiles/ || echo "No profiles found"

echo ""
echo "Generated logs:"
ls -la /project/test_logs/ || echo "No logs found"

# Look for our hybrid unwinding in the logs
if ls /project/test_logs/DD-DotNet-Profiler-Native-*.log >/dev/null 2>&1; then
    echo ""
    echo "=== Searching for hybrid unwinding evidence ==="
    
    echo "Looking for 'hybrid' mentions:"
    grep -i "hybrid\|CollectStackHybrid\|IsManagedCode" /project/test_logs/DD-DotNet-Profiler-Native-*.log || echo "No hybrid unwinding messages found"
    
    echo ""
    echo "Looking for stack collection messages:"
    grep -i "stack\|unwind" /project/test_logs/DD-DotNet-Profiler-Native-*.log | head -10 || echo "No stack/unwind messages found"
    
    echo ""
    echo "Looking for errors:"
    grep -i "error\|fail\|exception" /project/test_logs/DD-DotNet-Profiler-Native-*.log | head -5 || echo "No errors found"
    
    echo ""
    echo "=== Log file summary ==="
    echo "Log file size: $(wc -l /project/test_logs/DD-DotNet-Profiler-Native-*.log)"
    echo "First 5 lines:"
    head -5 /project/test_logs/DD-DotNet-Profiler-Native-*.log
    echo "Last 5 lines:"
    tail -5 /project/test_logs/DD-DotNet-Profiler-Native-*.log
else
    echo "No profiler log files found!"
fi

echo ""
echo "=== Test completed ==="
