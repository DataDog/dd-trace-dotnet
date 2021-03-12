#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

cd "$DIR/../.."

cd src/Datadog.Trace.ClrProfiler.Native
mkdir -p build
(cd build && cmake ../ -DCMAKE_BUILD_TYPE=Release && make)

mkdir -p bin/Release/x64
cp -f build/bin/Datadog.Trace.ClrProfiler.Native.so bin/Release/x64/Datadog.Trace.ClrProfiler.Native.so
