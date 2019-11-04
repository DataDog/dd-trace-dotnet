#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
VERSION=1.9.0

mkdir -p $DIR/../deploy/linux
cp $DIR/../integrations.json $DIR/../src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/

cd $DIR/../deploy/linux
for pkgtype in deb rpm tar ; do
    fpm \
        -f \
        -s dir \
        -t $pkgtype \
        -n datadog-dotnet-apm \
        -v $VERSION \
        $(if [ $pkgtype != 'tar' ] ; then echo --prefix /opt/datadog ; fi) \
        --chdir $DIR/../src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64 \
        netstandard2.0/ \
        Datadog.Trace.ClrProfiler.Native.so \
        integrations.json
done

gzip -f datadog-dotnet-apm.tar
mv datadog-dotnet-apm.tar.gz datadog-dotnet-apm-$VERSION.tar.gz
