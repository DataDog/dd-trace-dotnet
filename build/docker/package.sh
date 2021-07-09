#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
VERSION=1.27.1
BUILD_TYPE=${buildConfiguration:-Debug}
ARCH=${ARCHITECTURE:-x64}

mkdir -p $DIR/../../deploy/linux
cp $DIR/../../integrations.json $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64/
cp $DIR/../../build/artifacts/createLogPath.sh $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64/

# If running the unified pipeline, copy managed assets now instead of in the profiler build step
if [ -n "${UNIFIED_PIPELINE-}" ]; then
  mkdir -p $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64/netstandard2.0
  cp $DIR/../../src/bin/windows-tracer-home/netstandard2.0/*.dll $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64/netstandard2.0/

  mkdir -p $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64/netcoreapp3.1
  cp $DIR/../../src/bin/windows-tracer-home/netcoreapp3.1/*.dll $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64/netcoreapp3.1/
fi

cd $DIR/../../deploy/linux
for pkgtype in $PKGTYPES ; do
    fpm \
        -f \
        -s dir \
        -t $pkgtype \
        -n datadog-dotnet-apm \
        -v $VERSION \
        $(if [ $pkgtype != 'tar' ] ; then echo --prefix /opt/datadog ; fi) \
        --chdir $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/${BUILD_TYPE}/x64 \
        netstandard2.0/ \
        netcoreapp3.1/ \
        Datadog.Trace.ClrProfiler.Native.so \
        integrations.json \
        createLogPath.sh
done

gzip -f datadog-dotnet-apm.tar

if [ -z "${MUSL-}" ]; then
  if [ "$ARCH" == "x64" ]; then
    mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION.tar.gz
  else
    mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION.$ARCH.tar.gz
  fi
else
  if [ "$ARCH" == "x64" ]; then
    mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION-musl.tar.gz
  else
    mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION-musl.$ARCH.tar.gz
  fi
fi
