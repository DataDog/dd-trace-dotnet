#!/bin/bash

source common_build_functions.sh

if [ -n "$CI_COMMIT_TAG" ] && [ -z "$DOTNET_PACKAGE_VERSION" ]; then
  DOTNET_PACKAGE_VERSION=${CI_COMMIT_TAG##v}
fi

if [ -z "$ARCH" ]; then
  ARCH=amd64
fi

TMP_DIR=$(mktemp --dir)

curl --location --fail \
  --output $TMP_DIR/datadog-dotnet-apm.old \
  "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$DOTNET_PACKAGE_VERSION/datadog-dotnet-apm_${DOTNET_PACKAGE_VERSION}_$ARCH.deb"

fpm --input-type deb \
  --output-type dir \
  --name datadog-dotnet-apm \
  --package $TMP_DIR \
  datadog-dotnet-apm.old

echo -n $DOTNET_PACKAGE_VERSION > $TMP_DIR/opt/datadog/version/auto_inject-dotnet.version

# Build packages
datadog-package create \
    --version="$DOTNET_PACKAGE_VERSION" \
    --package="datadog-apm-library-dotnet" \
    --archive=true \
    --archive-path="datadog-apm-library-dotnet-$DOTNET_PACKAGE_VERSION-$ARCH.tar" \
    --arch "$ARCH" \ 
    --os "linux" \
    $TMP_DIR/datadog-dotnet-apm.dir/opt/datadog
