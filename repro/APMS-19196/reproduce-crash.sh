#!/bin/bash

echo "=== APMS-19196 Crash Reproduction Script ==="
echo "This script will reproduce the InvalidProgramException crash"
echo

# Setup WSL environment
echo "Setting up environment in WSL..."
cd ~
rm -rf ~/wsl-repro 2>/dev/null
mkdir -p ~/wsl-repro/datadog
cd ~/wsl-repro

# Download tracer v3.41.0 (buggy version)
echo "Downloading Datadog tracer v3.41.0..."
wget -q https://github.com/DataDog/dd-trace-dotnet/releases/download/v3.41.0/datadog-dotnet-apm-3.41.0.tar.gz
tar -C datadog -xzf datadog-dotnet-apm-3.41.0.tar.gz
find datadog -name '*.so' -exec chmod 755 {} \;
mkdir -p ~/logs/datadog/dotnet

# Copy source code
echo "Copying source code..."
cp -r /mnt/c/repositories/dd-trace-dotnet/repro/APMS-19196/*.csproj .
cp -r /mnt/c/repositories/dd-trace-dotnet/repro/APMS-19196/Program.cs .
cp -r /mnt/c/repositories/dd-trace-dotnet/repro/APMS-19196/Middlewares .

# Build app
echo "Building application..."
dotnet build -c Release

# Set crash-reproducing environment variables
echo "Setting up crash environment variables..."
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER='{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
export CORECLR_PROFILER_PATH=~/wsl-repro/datadog/Datadog.Trace.ClrProfiler.Native.so
export DD_DOTNET_TRACER_HOME=~/wsl-repro/datadog
export LD_PRELOAD=~/wsl-repro/datadog/linux-x64/Datadog.Linux.ApiWrapper.x64.so
export DD_INJECTION_ENABLED=tracer

# CRITICAL CRASH SETTINGS:
export DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true  # Key: Instruments at JIT time
export DD_EXCEPTION_REPLAY_ENABLED=true
export DD_TRACE_DEBUG=false                      # Key: No debug logs!

# Disable other features for clean reproduction
export DD_APPSEC_ENABLED=false
export DD_IAST_ENABLED=false
export DD_DUMP_ILREWRITE_ENABLED=false

# Minimal config
export DD_TRACE_LOG_DIRECTORY=~/logs/datadog/dotnet
export DD_TRACE_AGENT_URL=http://localhost:8126
export ASPNETCORE_URLS=http://localhost:5000
export ASPNETCORE_ENVIRONMENT=Production

echo
echo "=== STARTING APP - CRASH WILL OCCUR ON FIRST REQUEST ==="
echo "App will start normally, but every HTTP request will crash with InvalidProgramException"
echo "Press Ctrl+C to stop"
echo

# Start the app (this will crash on first HTTP request)
dotnet bin/Release/net9.0/ReproApp.dll