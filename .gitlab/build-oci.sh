#!/bin/bash

source common_build_functions.sh

if [ -n "$CI_COMMIT_TAG" ] && [ -z "$DOTNET_PACKAGE_VERSION" ]; then
  DOTNET_PACKAGE_VERSION=${CI_COMMIT_TAG##v}
fi

if [ -z "$ARCH" ]; then
  ARCH=amd64
fi

if [ "$ARCH" == "amd64" ]; then
  SUFFIX=""
elif [ "$ARCH" == "arm64" ]; then
  SUFFIX=".arm64"
else
  echo "Unsupported architecture: $ARCH"
  exit 1
fi

TMP_DIR=$(mktemp --dir)
 
if [ -n "$DOTNET_PACKAGE_SPECIFIC_VERSION" ]; then
  echo "Generating packages using local binaries. Set dev version: $DOTNET_PACKAGE_DEV_VERSION"
  DOTNET_PACKAGE_VERSION=${DOTNET_PACKAGE_DEV_VERSION}
  SRC_TAR="../artifacts/datadog-dotnet-apm-$DOTNET_PACKAGE_SPECIFIC_VERSION${SUFFIX}.tar.gz"

  if [ ! -f $SRC_TAR ]; then
      echo "$SRC_TAR was not found!"
      exit 1
  fi

  cp $SRC_TAR $TMP_DIR/datadog-dotnet-apm.tar.gz
  
else
  curl --location --fail \
    --output $TMP_DIR/datadog-dotnet-apm.tar.gz \
    "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$DOTNET_PACKAGE_VERSION/datadog-dotnet-apm-${DOTNET_PACKAGE_VERSION}${SUFFIX}.tar.gz"
fi

# extract the tarball, making sure to preserve the owner and permissions
OUT_DIR=$TMP_DIR/datadog-dotnet-apm.dir
mkdir -p $OUT_DIR
tar --same-owner -pxvzf $TMP_DIR/datadog-dotnet-apm.tar.gz -C $OUT_DIR

echo -n $DOTNET_PACKAGE_VERSION > auto_inject-dotnet.version
cp auto_inject-dotnet.version $OUT_DIR/version

ls -l $OUT_DIR

# Build packages
datadog-package create \
    --version="$DOTNET_PACKAGE_VERSION" \
    --package="datadog-apm-library-dotnet" \
    --archive=true \
    --archive-path="datadog-apm-library-dotnet-$DOTNET_PACKAGE_VERSION-$ARCH.tar" \
    --arch "$ARCH" \
    --os "linux" \
    $OUT_DIR
