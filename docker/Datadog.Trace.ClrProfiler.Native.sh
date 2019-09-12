#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

cd "$DIR/.."

PUBLISH_OUTPUT="$( pwd )/src/bin/managed-publish/netstandard2.0"

cd src/Datadog.Trace.ClrProfiler.Native
mkdir -p obj/Debug/x64
(cd obj/Debug/x64 && cmake ../../.. && make)

mkdir -p bin/Debug/x64
cp -f obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so bin/Debug/x64/Datadog.Trace.ClrProfiler.Native.so

mkdir -p bin/Debug/x64/netstandard2.0
cp -f $PUBLISH_OUTPUT/*.dll bin/Debug/x64/netstandard2.0/
cp -f $PUBLISH_OUTPUT/*.pdb bin/Debug/x64/netstandard2.0/
