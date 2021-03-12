#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
VERSION=1.25.0

cd "$DIR/../.."
mkdir -p deploy/linux
cp integrations.json src/Datadog.Trace.ClrProfiler.Native/bin/Release/x64/
cp build/artifacts/createLogPath.sh src/Datadog.Trace.ClrProfiler.Native/bin/Release/x64/

mkdir -p deploy/linux/netstandard2.0
cp src/bin/tracer-home/netstandard2.0/*.dll deploy/linux/netstandard2.0/

mkdir -p deploy/linux/netcoreapp3.1
cp src/bin/tracer-home/netcoreapp3.1/*.dll deploy/linux/netcoreapp3.1/

cd deploy/linux
for pkgtype in $PKGTYPES ; do
    fpm \
        -f \
        -s dir \
        -t $pkgtype \
        -n datadog-dotnet-apm \
        -v $VERSION \
        $(if [ $pkgtype != 'tar' ] ; then echo --prefix /opt/datadog ; fi) \
        --chdir $DIR/../../src/Datadog.Trace.ClrProfiler.Native/bin/Release/x64 \
        netstandard2.0/ \
        netcoreapp3.1/ \
        Datadog.Trace.ClrProfiler.Native.so \
        integrations.json \
        createLogPath.sh
done

gzip -f datadog-dotnet-apm.tar

if [ -z "${MUSL-}" ]; then
  mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION.tar.gz
else
  mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION-musl.tar.gz
fi
