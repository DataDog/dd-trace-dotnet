#!/usr/bin/env bash

set -ex

export COR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
export CORECLR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
current_dir=$(pwd)
export COR_PROFILER_PATH="${current_dir}/${NATIVE_PROFILER_PATH}"
export CORECLR_PROFILER_PATH="${current_dir}/${NATIVE_PROFILER_PATH}"
export DD_DOTNET_TRACER_HOME="${current_dir}/${TRACER_HOME_PATH}"

cd ./dd-trace-dotnet/tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController
dotnet build $DOTNET_BUILD_ARGS

export DOTNET_GCHeapHardLimit=0x10000000  # 256MB
export DOTNET_GCHeapCount=0x2  # 2 heaps
dotnet ./bin/Debug/net9.0/Samples.AspNetCoreSimpleController.dll

