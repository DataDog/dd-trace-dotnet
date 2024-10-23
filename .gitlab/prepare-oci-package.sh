#!/bin/bash

set -eo pipefail

PACKAGE_VERSION=$(< ../artifacts/version.txt)

if [ -n "$CI_COMMIT_TAG" ] || [ -n "$DOTNET_PACKAGE_VERSION" ]; then
  VERSION=$PACKAGE_VERSION
else
  VERSION=$PACKAGE_VERSION$CI_VERSION_SUFFIX
fi

echo "VERSION=$VERSION"

if [ -z "$ARCH" ]; then
  ARCH=amd64
fi

if [ "$ARCH" == "amd64" ]; then
  if [ "$LIBC" == "musl" ]; then
    SUFFIX="-musl"
  else
    SUFFIX=""
  fi
elif [ "$ARCH" == "arm64" ]; then
  if [ "$LIBC" == "musl" ]; then
    SUFFIX="placeholder"
  else
    SUFFIX=".arm64"
  fi
else
  echo "Unsupported architecture: $ARCH"
  exit 1
fi

mkdir -p sources

if [ "$SUFFIX" != "placeholder" ]; then
  SRC_TAR="../artifacts/datadog-dotnet-apm-$PACKAGE_VERSION${SUFFIX}.tar.gz"

  if [ ! -f $SRC_TAR ]; then
     echo "$SRC_TAR was not found!"
     exit 1
  fi

  # extract the tarball, making sure to preserve the owner and permissions
  tar --same-owner -pxvzf $SRC_TAR -C sources
else
  touch sources/placeholder.txt
fi

echo -n $VERSION > sources/version

cp ../artifacts/requirements.json sources/requirements.json
