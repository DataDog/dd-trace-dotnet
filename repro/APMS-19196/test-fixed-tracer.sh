#!/bin/bash

echo "=== APMS-19196 Fixed Tracer Test Script ==="
echo "This script tests Steven's EH clause sorting fix - should NOT crash"
echo

# Setup WSL environment
echo "Setting up environment in WSL..."
cd ~
rm -rf ~/wsl-repro-fixed 2>/dev/null
mkdir -p ~/wsl-repro-fixed/logs/datadog/dotnet
cd ~/wsl-repro-fixed

# Use the FIXED tracer that we built (Steven's fix)
echo "Using FIXED tracer with Steven's EH clause sorting fix..."
FIXED_TRACER_HOME=/mnt/c/repositories/dd-trace-dotnet/shared/bin/monitoring-home

if [ ! -d "$FIXED_TRACER_HOME" ]; then
    echo "ERROR: Fixed tracer not found at $FIXED_TRACER_HOME"
    echo "Please ensure the tracer was built successfully first."
    exit 1
fi

# Copy source code
echo "Copying source code..."
cp -r /mnt/c/repositories/dd-trace-dotnet/repro/APMS-19196/*.csproj .
cp -r /mnt/c/repositories/dd-trace-dotnet/repro/APMS-19196/Program.cs .

# Build app
echo "Building application..."
dotnet build -c Release

# Kill any existing processes on port 5000
pkill -f "dotnet.*ReproApp" 2>/dev/null || true
lsof -ti:5000 2>/dev/null | xargs kill -9 2>/dev/null || true

# Set fixed-tracer environment variables
echo "Setting up FIXED tracer environment variables..."
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER='{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
export CORECLR_PROFILER_PATH=$FIXED_TRACER_HOME/linux-x64/Datadog.Trace.ClrProfiler.Native.so
export DD_DOTNET_TRACER_HOME=$FIXED_TRACER_HOME
export DD_INJECTION_ENABLED=tracer

# CRITICAL CRASH SETTINGS (same as reproduction, but using FIXED tracer):
export DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true  # Key: Instruments at JIT time
export DD_EXCEPTION_REPLAY_ENABLED=true
export DD_TRACE_DEBUG=false                      # Key: No debug logs!

# Disable other features for clean reproduction
export DD_APPSEC_ENABLED=false
export DD_IAST_ENABLED=false
export DD_DUMP_ILREWRITE_ENABLED=false

# Minimal config
export DD_TRACE_LOG_DIRECTORY=~/wsl-repro-fixed/logs/datadog/dotnet
export DD_TRACE_AGENT_URL=http://localhost:8126
export ASPNETCORE_URLS=http://localhost:5000
export ASPNETCORE_ENVIRONMENT=Production

echo
echo "=== STARTING APP WITH STEVENS FIXED TRACER ==="
echo "App should start normally and handle HTTP requests without InvalidProgramException"
echo "Press Ctrl+C to stop"
echo

# Start the app (this should NOT crash with Steven's fix)
dotnet bin/Release/net9.0/ReproApp.dll