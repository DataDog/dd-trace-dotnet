#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

export CORECLR_ENABLE_PROFILING="1"
export CORECLR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
export CORECLR_PROFILER_PATH="${DIR}/../src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so"
export DD_INTEGRATIONS="${DIR}/../integrations.json;/project/test-integrations.json"

eval "$@"