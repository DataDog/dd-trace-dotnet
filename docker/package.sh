#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
VERSION=0.4.0

mkdir -p $DIR/../deploy/linux

cd $DIR/../deploy/linux
for pkgtype in deb rpm tar ; do
    fpm \
        -f \
        -s dir \
        -t $pkgtype \
        -n datadog-dotnet-apm \
        -v $VERSION \
        --prefix /opt/datadog \
        --chdir $DIR/../src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64 \
        Datadog.Trace.ClrProfiler.Native.so
done

gzip -f datadog-dotnet-apm.tar
mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION.tar.gz
