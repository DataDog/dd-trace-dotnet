#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." >/dev/null && pwd )"

export CORECLR_ENABLE_PROFILING="1"
export CORECLR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
export CORECLR_PROFILER_PATH="${DIR}/src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so"
export DD_DOTNET_TRACER_HOME="${DIR}"
export DD_INTEGRATIONS="${DD_DOTNET_TRACER_HOME}/integrations.json"

mkdir -p /var/log/datadog/dotnet
touch /var/log/datadog/dotnet/dotnet-profiler.log
tail -f /var/log/datadog/dotnet/dotnet-profiler.log | awk '
  /info/ {print "\033[32m" $0 "\033[39m"}
  /warn/ {print "\033[31m" $0 "\033[39m"}
' &

eval "$@"
